# Ohirok.StepHashtags.ReqnrollPlugin

Adds step-level metadata to individual Gherkin steps, written as ordinary comments, and exposes it at runtime: to step definitions, hooks, scoped bindings and the report.

The syntax itself is parsed by the [PluginChains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md), and the other Ohirok plugins (`#retry`, `#xfail`, `#softassert`, …) read the same hashtags at generation time without requiring this package. This package is the optional runtime layer on top, and it is just as useful on its own, for your own hooks and step definitions, or as a base for your own plugins.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md): a plugin-authoring SDK and a set of Reqnroll plugins.

## Installation

```bash
dotnet add package Ohirok.StepHashtags.ReqnrollPlugin
```

The package includes the MSBuild registration, so the generator plugin is active without per-project wiring.

## What it does

Every comment above a step belongs to that step. Inside such a comment, a **hashtag** is a `#` at the start of the comment or after a space or tab, immediately followed by non-whitespace characters, up to the next space or tab. `# text` (with a space) is plain comment text, and so is a `#` inside a word (`issue#123`) or a URL fragment. Everything that is not a hashtag is ignored, so you can explain *why* in the same comment.

```gherkin
Scenario: External API call
    #timeout:5000
    #priority:high
    When I call the external service
    # Flaky sandbox, so #retry:2 for now, see #issue:123
    Then the response is valid
```

- `When` step: `["timeout:5000", "priority:high"]`
- `Then` step: `["retry:2", "issue:123"]`

### Rules

- **Which comments belong to a step.** All comment lines between the previous step (or, for the first step, the `Scenario` / `Background` line) and this step. Several lines stack. Blank lines do **not** break the block. Comments above the `Scenario` keyword, or after the last step, belong to no step.
- **Where the hashtag ends.** Tokens are split on whitespace only, so punctuation belongs to the token: `#retry:3.` is the hashtag `retry:3.` (write `#retry:3 .` or put the hashtag mid-sentence). `see issue #123` gives the hashtag `123`, while `issue#123` gives none; for structured data use `name:value`, e.g. `#issue:123`.
- **Raw tokens.** The plugin returns the tokens exactly as written, without the `#`. Case sensitivity and value parsing are up to whoever consumes them (check the README of each plugin for its own rules).
- **Where it works.** Scenario steps, `Background` steps, `Rule` background steps and `Scenario Outline` steps. In an outline the hashtags are literal text, shared by all examples: `<parameters>` are **not** substituted inside comments.
- **Quoting.** Quotes and backslashes in a hashtag are escaped when the test code is generated, so `#note:a"b` is safe.

At runtime the step sees its hashtags as `string[]`:

```csharp
using Ohirok.StepHashtags.ReqnrollPlugin;
using Reqnroll;

[When("I call the external service")]
public async Task CallService(ScenarioContext sc)
{
    var tags = sc.GetStepHashtags();
    // tags == ["timeout:5000", "priority:high"]

    var timeout = sc.GetStepHashtags(t => t.StartsWith("timeout:"))
        .Select(t => int.Parse(t.Substring("timeout:".Length)))
        .FirstOrDefault(5000);

    if (sc.HasStepHashtag("priority:high"))
    {
        // do something specific for high priority calls
    }
}
```

## API

```csharp
string[] tags = sc.GetStepHashtags();
string[] timeouts = sc.GetStepHashtags(t => t.StartsWith("timeout:"));

bool hasTag = sc.HasStepHashtag("priority:high");           // exact token, case-insensitive
bool hasAny = sc.HasStepHashtag(t => t.StartsWith("timeout:"));
```

## Native hooks

The current step's hashtags are available from `ScenarioContext` in `[BeforeStep]` and `[AfterStep]`, exactly as in the step body.

```csharp
[BeforeStep]
public void BeforeStep(ScenarioContext sc)
{
    if (sc.GetStepHashtags().Any(h => h.StartsWith("timeout:")))
    {
        // same metadata the step body sees
    }
}

[AfterStep]
public void AfterStep(ScenarioContext sc)
{
    if (sc.HasStepHashtag("ignore"))
    {
        // clean up step-local state
    }
}
```

## Using hashtags in `[Scope]`

The current step's hashtags are merged into the active tag set for that step, together with the scenario, outline, rule and feature tags. So a step definition can be selected by a hashtag through Reqnroll's normal `[Scope(Tag = "...")]`.

```gherkin
Scenario: Search
    #slow
    When I perform a simple search on "books"

Scenario: Quick search
    When I perform a simple search on "books"
```

```csharp
[When("I perform a simple search on {string}")]
public void Search(string term) { /* default */ }

[When("I perform a simple search on {string}"), Scope(Tag = "slow")]
public void SearchSlow(string term) { /* only for steps carrying #slow */ }
```

Reqnroll's rules for scoped bindings apply:

- `Scope` supports `Tag`, `Feature` and `Scenario`.
- Criteria inside one `[Scope]` are combined with `AND`.
- Several `[Scope]` attributes on the same method, or on the class and the method, are combined with `OR`.
- A scoped definition wins over an unscoped one; among scoped ones the most specific wins.
- Equally specific matches are reported as an **ambiguous binding**.

```csharp
[Scope(Tag = "a", Feature = "f")]    // tag a AND feature f
[Scope(Tag = "b")]                   // OR tag b
[Scope(Tag = "c")]                   // OR tag c
```

> **A hashtag can make a second definition match.** A hashtag that repeats a scenario tag changes nothing: `@web` with `#web` is still the single tag `web`. But a hashtag adds a tag the step did not have before, so a step in a `@web` scenario that carries `#slow` now matches both a `Scope(Tag = "web")` and a `Scope(Tag = "slow")` definition of the same text, each with one matching scope, and Reqnroll reports them as ambiguous. Give such definitions different step text, or scope one of them more narrowly (for example `Scope(Tag = "slow", Feature = "...")`). Tags are compared case-sensitively: `#Web` does not match `Scope(Tag = "web")`.

Binding selection by tag is the thing Reqnroll itself warns about (see "Danger" in the Reqnroll documentation on scoped bindings): use it for incidental concerns, not for business behavior. Reading hashtags inside one step definition (`sc.HasStepHashtag(...)`) is usually the simpler option.

## Built-in `#ignore`

```gherkin
Scenario: optional step
    # Disabled until BUG-123 is fixed.
    #ignore
    When I run a non-critical verification
```

The step is not executed. It is traced as skipped, a `StepSkipped` event is published, `-> skipped: <reason>` is added to the step output, and an issue of category `StepSkipped` (with the step's hashtags) is sent to the report.

By default the step status in the report is **not** changed (the step keeps the status the runner gave it). Set `markSkippedInReport` to show it as *Skipped*.

`#ignore` is the step-level counterpart of Reqnroll's native `@ignore` tag on a scenario.

### Why not just comment the step out?

The common alternative is to comment the step out. Comments in Gherkin exist to carry extra descriptions, not to hide code that doesn't work right now, and a commented-out step has several problems:

- **It is invisible to everything that is not a human reading the file.** The report shows nothing, so a skipped check looks the same as a check that never existed. An ignored step stays in the feature and in the report, with a reason.
- **Its step definition looks orphaned.** If the commented step was the only user of a definition, any "unused step definitions" script or review will point at that definition as dead code, and it can easily be deleted. The disabled step is then silently broken for good.
- **It drifts.** If a step is commented out in one place and still works in another, the definition is not orphaned, so it will be refactored. When its expression or attribute text changes, tools update the live usages and leave the commented one behind. Un-commenting it months later gives an undefined step, and nobody remembers what it was supposed to say.
- **It gets no checks.** A commented step is not matched against definitions, so typos, renamed parameters and removed bindings go unnoticed. An ignored step is still an ordinary step: it goes through binding resolution, so a definition that no longer matches shows up as an undefined step instead of rotting quietly.
- **It has no owner and no reason.** Commented-out code collects without explanation. A hashtag sits next to an ordinary comment in the same place, so the *why* (a bug id, a date, a condition) can be written as a sentence:

```gherkin
    # Flaky on the shared sandbox, #ignore until the new environment is ready.
    When I submit the payment
```

### Your own ignore logic

Besides `#ignore`, you can use your own logic to skip a step. Here is an example with a custom hashtag:

```gherkin
Scenario: Export
    # Needs a desktop session, so #local-only
    When I open the debugger visualization
```

```csharp
[BeforeStep]
public void SkipLocalOnlySteps(ScenarioContext sc)
{
    if (IsCi && sc.HasStepHashtag("local-only"))
        sc.ScenarioContainer.Resolve<StepExecutionContext>().Skip("local-only step on CI");
}
```

The skipped step gets the same treatment as `#ignore` (trace, `StepSkipped` event, output line, report issue) with the reason you pass. `ShouldSkip()` tells whether the step is already marked. `ignoreStep.enabled` only switches the built-in `#ignore` hashtag; skipping from your own code works either way.

The skip mechanism is not limited to hashtags, so any logic can be implemented in a `[BeforeStep]` hook. But remember that ignoring is for exceptional situations, so don't misuse it. Skipped steps must stay visible and traceable, both in the feature files and in logging and reporting: always pass a meaningful reason (the default `"user logic"` tells the reader nothing).

## Configuration

All options are optional; defaults are shown.

```json
{
  "stepHashtags": {
    "enabled": true,
    "output": "StdOut",
    "generationInterceptorOrder": -1000000000
  },
  "ignoreStep": {
    "enabled": true,
    "markSkippedInReport": false,
    "bindingInvokerOrder": 1000
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `stepHashtags.enabled` | `true` | Enables hashtag extraction and runtime handling. |
| `stepHashtags.output` | `StdOut` | `StdOut`: when a step starts, its hashtags are written to the step output as one line (`#retry:3 #note`). `None`: nothing is written. |
| `stepHashtags.generationInterceptorOrder` | `-1000000000` | Order of the generator interceptor that publishes a step's hashtags before the step's generated code runs. Kept the outermost by default so that every other plugin's injected code sees them (see the [SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md)). |
| `ignoreStep.enabled` | `true` | Enables the built-in `#ignore` hashtag. Skipping from your own code does not depend on it. |
| `ignoreStep.markSkippedInReport` | `false` | Mark an ignored step as *Skipped* in the report. |
| `ignoreStep.bindingInvokerOrder` | `1000` | Position of the `#ignore` invoker in the binding-invoker chain (see the [SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md)). |

The Ohirok report shows every step's hashtags regardless of `output`: it reads them from the feature file itself.

## Common use cases

- `#timeout:5000`: any metadata your own hooks and steps read (here, a step-specific timeout);
- `#retry:3`: retry a single step (see [Retry](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Retry/README.md));
- `#xfail:TimeoutException`: expected failure of a step (see [XFail](https://github.com/olesme/ohirok.reqnroll/blob/main/src/XFail/README.md));
- `#ignore`: skip one step without disabling the scenario;
- metadata relevant to one step only (issue links, notes for the report).

## When step-level hashtags are the lesser evil

I'm well aware step-level tags are generally considered a BDD anti-pattern, and I don't disagree with that, as a default.

If a difference in behavior belongs to the business specification, it should be expressed by the step itself. If a step is expected to throw because that's part of what the feature describes, I'd much rather write a step definition whose name says so than decorate an existing step with #xfail. Same with two steps that represent genuinely different business concepts — they deserve two different step definitions. That's what BDD is for, and hashtags aren't meant to replace it.

The situations I'm talking about are different. They're usually temporary: a flaky test environment, retries around an unreliable third-party service, a workaround for a known bug, some other incidental concern that has nothing to do with the business language. It's surprisingly common for this stuff to end up hidden inside a step definition anyway — branching logic, or a step that silently skips its own work under some condition, with nothing about it visible in the feature file. Someone reading the scenario has no way of knowing that the step they're looking at won't actually run as written. I think that's a worse outcome than a visible hashtag.

Comments are already legal in Gherkin. Comments above a step have always been valid Gherkin, and step hashtags simply give structured meaning to something already there. That means the feature file stays perfectly valid Gherkin — any existing parser, editor, formatter, or syntax highlighter keeps working exactly as before, even knowing nothing about Ohirok. The extra meaning only exists for plugins that choose to interpret it.

Because of that, hashtags don't have to look like configuration. They can read as ordinary documentation:

```gherkin
Scenario: Payment
    # This step needs #retry:3 because the payment sandbox is flaky in our test environment.
    When I submit the payment
    Then the payment is accepted

Scenario: Validation
    # Temporarily #xfail until BUG-123 is fixed.
    When I submit invalid data
    Then the validation error is shown
```

A person reads it as a normal comment explaining *why*. The plugins read the same line and know *what to do*. The explanation and the behavior live in the same place, and there's usually less code to write: instead of wrapper steps or conditionals buried in a binding, the feature file carries the "why" and the plugin handles the "how".

Scenario/Feature/Rule/Examples tags are already an accepted part of everyday BDD — people use them for environments, categories, retries, CI integration, issue tracking, and plenty of things that aren't really part of the business language either. Step hashtags don't introduce a new category of metadata; they just move the same idea closer to the one step it actually concerns, instead of spilling it over an entire scenario.

That doesn't mean every step should carry one. If most steps in a project end up wearing hashtags, that's a sign something else needs redesigning, not a sign you need more hashtags. The same is already true of comments themselves: Gherkin has always allowed a comment above any step, and nobody expects — or wants — every step to have one. Hashtags are meant to stay exactly that rare: an exception for an exceptional situation, not a second way of writing scenarios.

This also turned out to be one of the better ways to test the plugins themselves. Say a plugin wraps the binding invoker and intentionally changes how a step executes — to test that properly, I need one step to fail in a very specific, controlled way while the rest of the suite stays green. Wrapping that step in #xfail, with the right exception type and the right order around the plugin under test, turns the expected failure into part of the assertion instead of a red build.

If you still think step-level metadata has no place in BDD, while tags on a feature, rule, scenario, or outline examples set are fine, that's a completely reasonable position, and you don't have to use them. Nothing depends on them being present: without hashtags in your feature files, none of the plugins change anything.

## See also

- [PluginChains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md): where the hashtag syntax is parsed, and the generator/runtime chaining model.
- [XFail](https://github.com/olesme/ohirok.reqnroll/blob/main/src/XFail/README.md): `#xfail` / `#afail` built on step hashtags.
- [Retry](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Retry/README.md): retry metadata built on the same extension point.
