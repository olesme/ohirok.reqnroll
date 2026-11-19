# Ohirok.XFail.ReqnrollPlugin

Expected failures (`xfail`) and allowed failures (`afail`) for a single step.

Use it when a failure is known and should stay visible, but should not turn the run red: known bugs, an unreliable third-party service, a plugin you are testing that is supposed to fail in a controlled way.

The markers are written as comments above a step, using [StepHashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md), so the feature file stays valid Gherkin.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md): a plugin-authoring SDK and a set of Reqnroll plugins.

## Installation

```bash
dotnet add package Ohirok.XFail.ReqnrollPlugin
```

The package depends on StepHashtags and registers everything itself. No extra setup is needed.

## Two markers

| Marker | Meaning |
| --- | --- |
| `xfail` | The step **must** fail. Failing is the expected result; passing is an error. |
| `afail` | The step **may** fail or pass. Either way the scenario continues. |

Each marker has alternative spellings: `expect-fail` / `expectfail` for `xfail`, and `allow-fail` / `allowfail` for `afail`.

Use them as a step hashtag (`#xfail` above the step) whenever a step owns the failure - that is the normal case: the expectation sits exactly where the failure happens, and everything else in the scenario still has to pass. The same markers also exist as scenario tags (`@xfail`, `@afail`) for failures no step owns; see [Scenario tags](#scenario-tags-for-failures-outside-the-steps).

## Basic usage

### `#xfail`: the step must fail

```gherkin
Scenario: Known bug in validation
    Given I have invalid input
    #xfail:ArgumentException
    When I submit the form
    Then the error is logged
```

If the step throws a matching exception, the exception is caught, the step is reported as **passed** and the scenario continues (the caught exception is shown in the step output, see [Output](#output)). If the step passes, the step fails with `XFailUnexpectedPassException` (and an `XFailPassed` issue is added to the report). If it throws an exception that does not match, the test fails with `XFailWrongException` (the original exception is attached as the inner exception).

### `#afail`: the step may fail or pass

```gherkin
Scenario: Unstable external service
    #afail:TimeoutException|IOException
    When I call the third-party API
    Then the fallback mechanism ran
```

A matching exception is caught and the scenario continues; a pass is fine too. An exception that does not match fails the test with `XFailWrongException`.

### Type patterns

The text after the marker is part of a hashtag, so the [hashtag rules](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md#rules) apply: **no spaces**, and trailing punctuation becomes part of the pattern (`#xfail:ArgumentException.` will not match).

```text
#xfail:ArgumentException              the exception type
#afail:TimeoutException|IOException   several types, any match is accepted
#afail:*Exception                     anything ending in "Exception"
#afail:*Exception,!TimeoutException   wildcard minus exclusions
#xfail                                any exception
#afail                                any exception, or none
```

How the value is read:

- **After the keyword** you can use `:`, `=`, `-`, `_` or brackets, whichever you prefer: `#xfail:X`, `#xfail=X`, `#xfail-X`, `#xfail(X)`, `#xfail[X]`.
- **Several patterns** are separated by `|`, `,` or `;` (all equivalent).
- **Matching** is case-insensitive and is done against the exact type of the thrown exception, by short name (`ArgumentException`) or full name (`System.ArgumentException`). **Subtypes are not matched**: `#xfail:ArgumentException` does not accept an `ArgumentNullException`; list it or use a wildcard such as `Argument*`.
- **Wildcards:** `*` (any text) and `?` (one character). For full control use `regex:<pattern>` or `/<pattern>/`.
- **Exclusions:** a pattern starting with `!` is an exclusion. Exclusions win: if any exclusion matches, the exception is not accepted; if there are only exclusions, everything else is accepted.
- **Keyword match:** a hashtag is treated as a marker when it *starts with* the keyword (`xfail`, `expect-fail`, `expectfail`, `afail`, `allow-fail`, `allowfail`). Do not start your own hashtags with these words.

## Examples

```gherkin
# Known debt, visible in the feature itself
Scenario: legacy validation path
    #xfail:NotSupportedException
    When I call the old validation API
    Then the result is logged for follow-up

# Flaky dependency, the fallback path is what is tested
Scenario: external gateway is unreliable
    #afail:TimeoutException|IOException
    When I synchronise inventory
    Then the cached inventory state is still consistent
```

### Testing a plugin that is supposed to fail

Mark the step that must fail with `#xfail` and let the rest of the scenario verify the plugin changed behavior as expected. When the plugin under test fails the scenario outside any of your steps (from a hook or from code it injects), tag the scenario with `@xfail:<ExceptionType>` instead - see [Scenario tags](#scenario-tags-for-failures-outside-the-steps). If the plugin under test also wraps step execution, set the `Order` values explicitly so XFail wraps it from the outside (see the [SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md)).

## Scenario tags: for failures outside the steps

`@xfail` / `@afail` on a scenario (or on its rule, feature or Examples block) judge the whole test method instead of
one step: Before hooks, the steps, and steps or calls that other plugins add to the generated test - for example
SoftAssert's end-of-scenario flush. They take the same type patterns as the hashtags:

```gherkin
# The plugin under test must fail the scenario from its generated flush step, not from a step of ours.
@xfail:AggregateAssertionException
Scenario: Soft failures are reported at the end of the scenario
    #softassert
    When the price check fails
    Then the next step still runs
```

| Outcome of the test method | `@xfail` | `@afail` |
| --- | --- | --- |
| Fails with a matching exception | passes - *pass with issues* in the report | passes - *pass with issues* in the report |
| Passes | fails (`XFailUnexpectedPassException`) | passes |
| Fails with another exception | fails (`XFailWrongException`) | fails (`XFailWrongException`) |

Their main use is testing plugins that are supposed to make a scenario fail where no step can catch it. For a failure
in a step, prefer the hashtag on that step: a scenario tag also accepts the same failure coming from a different step,
so it says less about what is expected.

- `AfterScenario` hooks are **not** covered: Reqnroll runs them after the test method, outside the wrapper.
- **Do not combine** a scenario tag with step hashtags in the same scenario. A step's `#xfail` turns that step's failure
  into a pass before the scenario tag sees it, so the two describe the same failure twice and the scenario tag then
  reports an unexpected pass.
- The scenario wrapper sits outside Retry's (`generationInterceptorOrder` -1000000 vs. Retry's 50): a retried scenario is
  judged on its final attempt.

## Hooks

Run your own code when an expected or allowed failure is observed:

```csharp
using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;
using Reqnroll;

[Binding]
public class MyXFailHooks
{
    [OnXFail]
    public void WhenXFailed(XFailScenarioContext ctx)
    {
        // ctx.Mode      XFail or AFail
        // ctx.Failed    true if the step threw
        // ctx.Passed    true if it passed (possible for afail only)
        // ctx.Exception the caught exception, or null
    }

    [OnAFail("threw:TimeoutException")]
    public void WhenTimeoutAllowed(XFailScenarioContext ctx)
    {
        // runs only when the filter matches
    }
}
```

- `[OnXFail]` runs when an `xfail` step failed as expected, `[OnAFail]` for `afail` steps (failed or passed).
- Parameters: `XFailScenarioContext`, or anything registered in the Reqnroll container. The method may be `static` or return `Task`.
- A hook may carry several filters (`[OnXFail("a", "b")]`) and the attribute can be repeated. A hook without filters always runs.

Values a filter can match:

| Value | Example |
| --- | --- |
| The step's hashtags, as written | `timeout:5000` |
| The part after the marker | `#xfail:ArgumentException` gives `ArgumentException` |
| Outcome | `passed`, `failed` |
| Mode | `xfail`, `afail` |
| Exception type name | `threw:X`, `exception:X`, `ex:X`, `error:X`, or just `X` |

## Output

An observed expected failure is echoed to the step output. Set `outputMode` to control it:

- `Brief` (default): `Expected failure hit: ArgumentException: <message>`;
- `Full`: the whole exception with stack trace;
- `None`: nothing.

For `afail`, a step that passed prints `Allowed failure but step passed.`

## Configuration

All options are optional; defaults are shown.

```json
{
  "xfail": {
    "enabled": true,
    "outputMode": "Brief",
    "bindingInvokerOrder": 2000,
    "generationInterceptorOrder": -1000000
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the plugin off completely. |
| `outputMode` | `Brief` | `None`, `Brief` or `Full` (see above). |
| `bindingInvokerOrder` | `2000` | Position of the invoker in the binding-invoker chain. |
| `generationInterceptorOrder` | `-1000000` | Position of the scenario-tag wrapper among generation interceptors; lower wraps outside. |

The order value only matters when several plugins wrap the same step; see the [SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md).

## Finding the marked steps

For a list of all marked steps and scenarios (for a review, or for a CI check), search the feature files, for example in PowerShell:

```powershell
Get-ChildItem -Recurse -Filter *.feature |
  Select-String -Pattern '[#@](xfail|expect-?fail|afail|allow-?fail)\b' |
  ForEach-Object { "{0}:{1}  {2}" -f $_.Path, $_.LineNumber, $_.Line.Trim() }
```

To filter whole scenarios in a test run, use ordinary tags of your own (for example `@known-bug`) next to the marker.

## Custom exception types

By default, an unexpected pass throws `XFailUnexpectedPassException` and a wrong type throws `XFailWrongException`. To use your own:

```csharp
// before the tests run, for example in a static [BeforeTestRun] hook
XFailOptions.UseExceptionTypes<MyUnexpectedPassException, MyWrongExceptionType>();
```

`MyUnexpectedPassException` needs a public `(string message)` constructor and `MyWrongExceptionType` a public `(string message, Exception inner)` constructor; otherwise an `ArgumentException` is thrown. For full control, implement `IXFailExceptionFactory` and assign it:

```csharp
XFailOptions.ExceptionFactory = new MyExceptionFactory();
```

## See also

- [PluginChains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md): the generator/runtime chaining model.
- [StepHashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md): how step hashtags are extracted and read.
