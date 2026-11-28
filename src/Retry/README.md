# Ohirok.Retry.ReqnrollPlugin

Retry support for Reqnroll: whole scenarios, individual steps, and step groups.

This package is a practical retry layer for flaky Reqnroll tests.

It works with NUnit, xUnit (v2 and v3), MSTest, and TUnit. Because it doesn’t replace the Unit Test Provider, it is runner-agnostic and is expected to work with any other or custom provider, though those aren’t tested.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md) — a plugin-authoring SDK and a set of Reqnroll plugins.

## Installation

```bash
dotnet add package Ohirok.Retry.ReqnrollPlugin
```

A test project must reference exactly one `Reqnroll.<runner>` package; two of them fail with "Unit test Provider already set".

## How attempts are counted

Retry libraries disagree on what the number means. NUnit's `[Retry(3)]` and JUnit Pioneer's `@RetryingTest(3)` count **attempts**: at most 3 runs in all. Playwright's `retries: 3`, pytest-rerunfailures' `--reruns 3` and Polly's `MaxRetryAttempts = 3` count **retries**: the first run plus up to 3 more. Here the keyword says which one you mean:

| You write | Means | Runs a failing scenario at most |
| --- | --- | --- |
| `@retry:2`, `@retries:2`, `@retryblock:2`, `#retry:2` | 2 retries after the first run | 3 times |
| `@attempts:3`, `@tries:3`, `@3attempts` | 3 attempts, the first run included | 3 times |
| `@retry:0`, `@attempts:1`, `@noretry` | no retry | once |
| `@retry` (no number) | the configured default, `retry.maxRetries` / `retry.maxAttempts` | 3 times unless configured |

Both spellings set the same single value, so they combine like any other tag: `@retry @attempts:5` turns retry on and allows 5 runs. `RetryContext` shows it both ways, as `MaxAttempts` and `MaxRetries`.

`@noretry` switches retry off for one scenario, rule or `Examples` block under a `@retry` feature; as with every other setting, the most specific tag wins.

Only real failures are retried. A pending step (`PendingStepException`), a step with no definition and an ambiguous step fail the same way on every attempt, so the loop stops at the first one, whatever the tag says (an `OnRetryDecision` hook can still force a retry).

A scenario that passes only after a retry is reported as passed with an issue (`RetrySucceeded`) in the Ohirok report, so flaky tests stay visible.

## Scenario retry

Tag a scenario (or a Feature, a Rule, or an `Examples` block) with `@retry:N`:

```gherkin
@retry:3
Scenario: Flaky test
    When the unstable operation happens
    Then the result is correct
```

With timeout and delay:

```gherkin
@retry:5 @timeout:30s @delay:500ms
Scenario: Slow external dependency
    When I poll for the result
    Then it has appeared
```

A failed scenario is re-run from the start: `ScenarioContext` is recreated and `BeforeScenario`/`AfterScenario` hooks run for every attempt. An `AfterScenario` hook that fails while a retried attempt ends does not stop the retry: the failure is shown as a warning on that attempt and the remaining `AfterScenario` hooks still run. On the final attempt a failing hook fails the test as usual. Retry is activated by a tag that starts with `retry` (or names retries number first, `@2retries`); `@attempts:5`, `@timeout:30s` and the like only tune it. On steps, use `#retry` / `#retryblock`.

A tag on a Feature or Rule applies to all its scenarios. When levels conflict, the more specific one wins (Feature, Rule, Scenario, Examples); conditions from all levels are combined.

**Scenario Outline.** Each example row is its own scenario and is retried only if its combined tags contain a retry tag. Put `@retry` on an `Examples` block to retry just that block, or on the outline to retry all rows.

## Tag grammar

A value follows its keyword after a delimiter (`:`, `=`, `(`, `[`, `_`, `-`). Tags work with `@` on scenarios and with `#` on steps.

| What | Keywords | Examples |
| --- | --- | --- |
| Retries | `retry`, `retries`, `retryblock` | `@retry:2`, `@retry(2)`, `@retries=2`, `@2retries` |
| Attempts | `attempts`, `tries` | `@attempts:3`, `@tries=3`, `@3attempts` |
| Off | `noretry` | `@noretry` |
| Timeout | `timeout`, `limit`, `maxtime` | `@timeout:30s`, `@limit=2min`, `@30s` |
| Delay | `delay`, `wait`, `pause`, `interval`, `sleep` | `@delay:500ms`, `@wait=2s` |
| Conditions | `conditions`, `condition`, `cond`, `on`, `except`, `if`, `when`, `for` | `@on:TimeoutException`, `@conditions[IOException,Timeout*]` |

- **Units:** `ms` / `milli*` = milliseconds, `s` / `sec*` = seconds, `m` / `min*` = minutes. A number after a keyword without a unit is milliseconds.
- **Number first:** `@2retries`, `@3attempts` and `@500ms`, `@2min` (timeout) are self-describing. A bare number (`@7`) is ignored, and so is `@3times`: it could mean either count. A delay always needs its keyword.
- **Conditions** are exception type names; a type matches itself and its base types, case-insensitively. `*` and `?` are wildcards, `!` excludes (`@on:!ArgumentException` retries everything except that), and a dotted pattern matches the full type name. Separators between values: `,` `;` `|` and the delimiters above. A tag without a delimiter after the keyword (`@online`, `@format`, `@iffy`) is not a condition.

## Step retry

Annotate individual steps with `#retry:N`:

```gherkin
Scenario: Mixed reliability
    When this step is reliable
    #retry:3
    When this step might fail
    Then the final state is good
```

With a condition and a time limit:

```gherkin
Scenario: Timeout-specific retry
    #retry:5 #condition:TimeoutException #timeout:10s
    When the network call goes out
    Then the response arrived
```

The timeout is the total budget of the loop, checked between attempts; a running step is not interrupted. When it runs out, every retry type (scenario, block and step) fails the same way: a `TimeoutException` whose message states the timeout and the last error, e.g. `Retry Step timed out after 1.2s (2 attempts, limit: 1.0s). Last error: InvalidOperationException: ...`, with that last error as its `InnerException`. The same line is written to the scenario output and shown in the Ohirok report as a `RetryTimedOut` issue.

Hashtags next to `#retry` / `#retryblock` on the same step (`#timeout`, `#condition`, `#delay`, `#attempts:3`) tune that step or block the way tags tune a scenario.

## Block retry

Retry a group of steps together with `#retryblock:N`. The block ends at `#end`, at the next `#retry…` step, or at the end of the scenario:

```gherkin
Scenario: Transactional steps
    Given the precondition is met
    #retryblock:3
    When step one modifies state
    And step two depends on step one
    #end
    Then the final outcome is verified
```

After a failure the remaining steps of the attempt are skipped and the block starts again from its first step. Nothing outside the block is re-run.

`@retryblock:N` as a scenario tag treats all steps of the scenario (Background included) as one block, without restarting the scenario itself: hooks and `ScenarioContext` stay.

**Retries do not nest.** While a retry is active, step and block retry markers inside it are ignored: the outermost one wins.

## RetryContext

`RetryContext` is available during retried scenarios, blocks and steps via `ScenarioContext.GetRetryContext()` (namespace `Ohirok.Retry.Extensions`), and as a parameter of lifecycle hooks:

```csharp
var retry = sc.GetRetryContext();
if (retry != null)
{
    Console.WriteLine($"Attempt {retry.Attempt} of {retry.MaxAttempts}");
    Console.WriteLine($"Last exception: {retry.LastException?.Message}");
}
```

Key properties: `Type` (`Test`, `Block`, `Step`), `Attempt` (1 for the first run), `Repeat` (number of retries so far), `MaxAttempts` (runs allowed in all), `MaxRetries` (`MaxAttempts` - 1), `Status`, `IsSuccess`, `IsFailed`, `LastException`, `FirstException`, `HookFailures` (`AfterScenario` hook failures of the attempts that were retried; one on the final attempt fails the test as usual), `Elapsed`, `AttemptElapsed`, `Timeout`, `Delay`, `Conditions`, `FeatureTags`, `RuleTags`, `ScenarioTags`, `Hashtags`, `AllTags`.

`RetryContext` is also a key/value store (`retry["startedAt"] = …`); values live for the whole loop, across all attempts.

## Passing state to the next attempt

Scenario retry recreates `ScenarioContext`. To carry values forward:

```csharp
sc.MigrateToNextAttempt();               // everything currently in the context
sc.AddToNextAttempt("counter", attempts); // one key
sc.SetInNextAttempt(myState);             // one typed value (keyed by type name)
```

Block and step retry run inside the same scenario, so nothing is reset and these calls are not needed.

## Lifecycle hooks

```csharp
using Ohirok.Retry.Attributes;
using Ohirok.Retry.Core;

[Binding]
public class RetryHooks(ScenarioContext sc, IReqnrollOutputHelper output)
{
    [BeforeRetryLoop(RetryType.Test)]
    public void BeforeLoop(RetryContext retry) =>
        output.WriteLine($"Retry loop started for '{sc.ScenarioInfo.Title}'.");

    [OnRetryDecision("ex:HttpRequestException")]
    public void Decide(RetryContext retry)
    {
        if (retry.LastException?.Message.Contains("503") == true)
            retry.ForceRetry();   // or retry.ForceStop()
    }

    [OnRetry]
    public void BeforeNextAttempt(RetryContext retry) =>
        output.WriteLine($"Retrying (attempt {retry.Attempt + 1})...");

    [AfterRetryLoop("result:failure")]
    public void AfterLoop(RetryContext retry) =>
        output.WriteLine("All attempts exhausted.");
}
```

| Attribute | Fires |
| --- | --- |
| `BeforeRetryLoop` | Once, when the loop starts. |
| `OnRetryDecision` | After a failed attempt, before the verdict. `ForceRetry()` / `ForceStop()` override it for this attempt (the last hook wins). Still bounded by `MaxAttempts` and timeout. |
| `OnRetry` | When a retry is certain, before the next attempt. |
| `AfterRetryLoop` | Once, when the loop ends. |

Hook methods are resolved from the scenario container, so they can take any registered service as a parameter; `RetryContext` is injected.

**Filters** are constructor arguments. An optional `RetryType` comes first, then patterns:

```csharp
[BeforeRetryLoop(RetryType.Step, "#integration")]
[OnRetry("@slow", "!@quarantine", Order = 5)]
```

A hook runs when at least one positive pattern matches (or there are none) and no `!` pattern matches. Patterns are case-insensitive and support `*` and `?`. Available values:

- tags and hashtags: `@tag`, `#hashtag` (or the bare name);
- type: `test`, `block`, `step`, `type:step`;
- outcome (after the loop ended): `success` / `pass`, `failure` / `fail`, `result:success`, `result:failed`, `result:timedout`;
- exception: `ex:Type`, `exception:Type`, `threw:Type`, `error:Type`, or the bare type name.

`Order` sorts hooks of the same kind (lower first, default 10000).

## [RetryStep] attribute

For step-level retry without feature file annotations:

```csharp
[RetryStep(MaxRetries = 2, TimeoutMs = 5000, DelayMs = 200)]
[When("the polling step")]
public async Task Poll() { ... }
```

Properties: `MaxRetries` or `MaxAttempts` (the same count two ways, as for tags; set one), `Condition`, `Conditions`, `TimeoutMs`, `DelayMs`, and `Tags` (extra tags exposed to hook filters). Unset values fall back to the configuration.

## Working with other plugins

Ohirok plugins do not know about each other; an absent plugin simply has no effect.

- **XFail.** A `#xfail` step that fails as expected is not a failure for Retry. A violated expectation is an ordinary failure and is retried like any other; add a condition on the exception type if you do not want that.
- **SoftAssert.** A scenario that fails only because of soft failures is retried by `@retry`: the flush is a regular step in the test body. For `#retry` + `#softassert` on one step, Retry runs first and SoftAssert captures what remains. Step and block retry react to thrown exceptions only, so a soft failure recorded directly in step code does not trigger them; test-level retry does. If a step records soft failures and then throws, the failed attempt's soft failures would still fail the scenario after a successful retry. Discard them with Catchy's checkpoint:

```csharp
[Binding]
public class SoftRetryHooks(StatefulAsserter asserter)
{
    [BeforeRetryLoop(RetryType.Step)]
    [BeforeRetryLoop(RetryType.Block)]
    public void Mark() => asserter.Soft.SoftState.Checkpoint();

    [OnRetry(RetryType.Step)]
    [OnRetry(RetryType.Block)]
    public void Rollback() => asserter.Soft.SoftState.Revert();
}
```

## Events

With `publishEvents` enabled, the plugin also publishes `RetryLoopStartedEvent`, `RetryDecisionEvent`, `RetryRestartedEvent` and `RetryLoopCompletedEvent` (namespace `Ohirok.Retry.Events`) to Reqnroll's event pipeline.

## Configuration

All settings are optional; omitted ones use the defaults below. Settings are read through the shared plugin configuration (see the `PluginChains.Sdk` README) from the `retry` section:

```json
{
  "retry": {
    "maxAttempts": 3,
    "delay": 500,
    "condition": "TimeoutException"
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `maxRetries` | `2` | Retries after the first run when a tag or attribute gives no number. |
| `maxAttempts` | `3` | The same default counted as attempts (the first run included). Set `maxRetries` or `maxAttempts`; if both are set they must agree. |
| `delay` | `0` | Delay between attempts, in milliseconds. |
| `condition` | `""` | Global retry condition when no tag or attribute specifies one. |
| `conditions` | `[]` | Same, for several conditions; takes precedence over `condition`. |
| `allowRetryTag` | `true` | Enables `@retry` / `@retryblock` tags. |
| `allowRetryHashtag` | `true` | Enables `#retry` / `#retryblock` step hashtags. |
| `allowRetryStepAttribute` | `true` | Enables `[RetryStep]`. |
| `publishEvents` | `false` | Publishes the retry events listed above. |
| `generationInterceptorOrder` | `50` | Order of the generator interceptor relative to other plugins. |
| `bindingInvokerOrder` | `0` | Position in the binding-invoker chain; lower is more outer. |

## See also

- `src/PluginChains.Sdk/README.md` for the shared generator/runtime chaining layer used by plugin families.
- `src/StepHashtags/README.md` for per-step metadata such as `#retry` and `#ignore`.
- `src/SoftAssert/README.md` for soft-assertion capture and flush.
- `src/XFail/README.md` for step-level expected-failure behavior built on the same patterns.
- `src/Reporting/README.md` for the report layer that surfaces retry outcomes in the generated HTML report.
