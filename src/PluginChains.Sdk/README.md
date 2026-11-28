# Ohirok.Reqnroll.PluginChains.Sdk

An SDK for building Reqnroll plugins that cooperate instead of replacing each other.

Reqnroll lets a plugin take over a runtime or generator extension point, but not share it. This SDK adds the missing composition layer: chained generator interceptors and runtime wrappers with a defined order, attribute-driven custom hooks, shared configuration, and report signals. It does not depend on any particular plugin.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md): this SDK and a set of Reqnroll plugins built on it (StepHashtags, XFail, Retry, SoftAssert, Variants, AutoTags, IgnoreReasons, PersistentCleanup, Reporting). Those plugins are complete tools you can use on their own, and their source is a working reference for writing plugins of your own; the examples below point to them.

Use the SDK when you need to:

- wrap a generator hook or runtime invoker without the last plugin overwriting the previous one;
- register multiple interceptors in a predictable, configurable order;
- rewrite a feature before generation, with the Cucumber messages following the rewritten feature;
- discover custom runtime bindings and hooks in a shared way;
- read plugin settings from `reqnroll.json`;
- signal report issues without depending on a reporting plugin.

## The problem

Reqnroll allows a plugin to replace a single runtime or generator dependency in the BoDi container, but it does not provide a built-in chain. If several plugins want to wrap the same binding invoker or generation pipeline, the last one registered wins.

This SDK solves that by providing:

- a generator-interceptor registry that supports multiple interceptors,
- a runtime chaining API for `IAsyncBindingInvoker` and other container links,
- a custom-binding discovery layer for attribute-based runtime hooks and bindings,
- shared configuration and reporting helpers.

## Conventions for plugins

These are the conventions the Ohirok plugins follow. Adopt them in your own plugins so that they combine with others:

- **No plugin knows about another.** An absent plugin simply has no effect; it never breaks a run. Plugins meet only through Reqnroll itself (steps, hooks, `ScenarioContext`), through the ordering below, and through the reporting signals.
- **A plugin implements the common basics.** Edge cases are left to the user through lifecycle hooks.
- **Shared concerns live in the SDK**, not in the plugins: ordering, configuration, hook filters, report markers, escaping of generated code.

## Generator interceptors

Interceptors add code to the generated test methods. There are two levels.

**`GenerationInterceptor`** is the low-level base class; override the hooks you need:

| Hook | Generated code goes |
| --- | --- |
| `BeforeTestClass` / `AfterTestClass` | around the test class |
| `BeforeScenarioMethod` / `AfterScenarioMethod` | start and end of the scenario method body |
| `BeforeFeatureBackgroundSteps` / `AfterFeatureBackgroundSteps` | inside the feature background method |
| `BeforeRuleBackgroundSteps` / `AfterRuleBackgroundSteps` | rule background steps inlined in the scenario method |
| `BeforeAllScenarioSteps` / `AfterAllScenarioSteps` | all steps of the scenario method: background, rule background and own steps |
| `BeforeScenarioOwnSteps` / `AfterScenarioOwnSteps` | the scenario's own steps only |
| `BeforeStep` / `AfterStep` | around each step |

Generated code can use the `testRunner` field of the test class. Add code with `ctx.Add("...")`.

**`TransformDocument(document)`** runs before any of them: it returns the feature to generate from, the given one or a changed copy (for example a scenario turned into a Scenario Outline with extra Examples). The interceptors run it in ascending `Order`, each receiving the previous result. The test code and the Cucumber messages (Gherkin document, pickles) are both built from the result, so tests and reports always agree; the `.feature` file is not changed. [Variants](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Variants/README.md) and [AutoTags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/AutoTags/README.md) (tags added to scenarios, which then reach runner categories and tag-scoped hooks too) are built on it, and so is [IgnoreReasons](https://github.com/olesme/ohirok.reqnroll/blob/main/src/IgnoreReasons/README.md), which also changes the generated ignore attribute in `AfterScenarioMethod`.

**`FluentInterceptor`** (`Ohirok.GenerationInterceptor.Fluent`) is built on top of it and gives you the steps of a block together with their hashtags. Override `OnScenario`, `OnFeatureBackground`, `OnRuleBackground` or `OnTestClass`:

`````csharp
internal sealed class MyInterceptor : FluentInterceptor
{
    public override int Order => 100;
    protected override IReadOnlyList<string> TagPrefixes => ["mytag"];

    protected override void OnScenario(ScenarioBlock scenario) => Mark(scenario.Steps);
    protected override void OnFeatureBackground(FeatureBackgroundBlock bg) => Mark(bg.Steps);
    protected override void OnRuleBackground(RuleBackgroundBlock bg) => Mark(bg.Steps);

    private static void Mark(StepBlockInfo steps)
    {
        foreach (var step in steps.Where(s => s.HasHashtag("mytag")))
            step.InjectBefore("My.Plugin.Manager.Mark(testRunner);");

        foreach (var seq in steps.FindSequences(
                     from: s => s.HasHashtag("begin"),
                     until: s => s.HasHashtag("end")))
            seq.WrapSequence("await My.Plugin.Manager.RunAsync(testRunner, async () => {", "});");
    }
}
`````

`StepItem` exposes `Hashtags`, `HasHashtag`, `HasHashtagStartingWith`, `GetHashtagValue`, `InjectBefore`, `InjectAfter`, `Wrap`; `StepBlockInfo` adds `FindSequence(s)`, `WrapAll`; `StepSequence` adds `WrapSequence` and `WrapEach`. A fluent interceptor seals the hooks for the own-steps, backgrounds and single steps, but the other hooks (for example `BeforeScenarioMethod` or `AfterAllScenarioSteps`) stay overridable, so you can combine both levels.

Register the interceptor from a generator plugin, and initialize the configuration there:

`````csharp
public void Initialize(GeneratorPluginEvents events, GeneratorPluginParameters parameters,
    UnitTestProviderConfiguration unitTestProviderConfiguration)
{
    PluginConfigProvider.Initialize(events);   // required to read reqnroll.json in the generator; reloads per project

    events.CustomizeDependencies += (_, args) =>
        InterceptorRegistry.Register(args.ObjectContainer, new MyInterceptor());
}
`````

### Escaping

Anything user-controlled that you put into generated code as a string literal (hashtags, tag values, step text) must be turned into a valid C# literal. Use `GeneratedCode` (`Ohirok.GenerationInterceptor`) instead of escaping by hand:

`````csharp
GeneratedCode.StringLiteral("a\"b\\c");                // "a\"b\\c", quotes included
GeneratedCode.StringArray(step.Hashtags);              // new string[] { "retry:3", "note:a\"b" }

step.InjectBefore($"My.Plugin.Manager.Mark(testRunner, {GeneratedCode.StringArray(step.Hashtags)});");
`````

The literals are produced by the C# compiler's own code generator, so any text, including quotes, backslashes, control characters and non-ASCII, compiles and round-trips exactly.

## Step hashtags

The generator reads **step hashtags**: metadata written as ordinary Gherkin comments above a step. They are handed to your interceptors, so a plugin gets them without depending on any other plugin. Several Ohirok plugins (XFail, SoftAssert, Retry) are driven by them. For the user-facing description see the [StepHashtags README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md).

`````gherkin
Scenario: External API call
    #timeout:5000 #priority:high
    When I call the external service
    # Flaky sandbox, so #retry:2 for now
    Then the response is valid
`````

### Extraction rules

- **Which comments belong to a step.** All comment lines after the previous step of the same container (a `Scenario`, `Scenario Outline`, `Background` or Rule background) and before this step. For the first step of a container, the lower bound is the `Scenario:` / `Background:` line, so comments above that keyword belong to no step. Blank lines do not break the block; several comment lines stack.
- **What a hashtag is.** A `#` that is the first character of the comment text or follows a space or tab, immediately followed by a non-whitespace character, up to the next space or tab. `# text` (with a space) is plain text, and so is a `#` inside a word or URL (`issue#123`, `http://x/#anchor`).
- **Raw tokens.** The token is returned without the `#`, exactly as written, case included. Punctuation belongs to the token (`#retry:3.` gives `retry:3.`). Structured values are up to the consumer; use `name:value`.
- **Outlines.** The hashtags of an outline step are literal text shared by all examples; `<parameters>` are not substituted in comments.
- **Hashtags from a plugin.** An interceptor that also implements `IStepHashtagSource` (`Ohirok.GenerationInterceptor.Core`)
  gives steps hashtags of its own: `GetStepHashtags(step)` is asked for every step, and what it returns is part of that
  step's hashtags for every interceptor and plugin, before the written ones (so for a setting read as its last value the
  written one wins). The feature file and the Cucumber messages do not change, so reports show only the written hashtags.
  [AutoTags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/AutoTags/README.md) uses it.

### Reading them in an interceptor

| Where | API |
| --- | --- |
| Low-level hooks (`BeforeStep` / `AfterStep`) | `ctx.Hashtags`, `ctx.HasHashtags` |
| Fluent interceptors | `StepItem.Hashtags`, `HasHashtag(tag)`, `HasHashtagStartingWith(prefix)`, `GetHashtagValue(prefix)` |
| Block / sequence search | `steps.Where(s => s.HasHashtag("x"))`, `steps.FindSequences(from: …, until: …)` |

`HasHashtag` and `HasHashtagStartingWith` compare case-insensitively; `GetHashtagValue(prefix)` returns what follows the prefix in the first matching hashtag.

Hashtags are also part of the tag set used for the per-scenario order override (`@mytag--order:N` works as a hashtag too).

### Passing them to generated code

`````csharp
foreach (var step in steps.Where(s => s.HasHashtagStartingWith("mytag")))
{
    var hashtags = step.Hashtags.Where(h => h.StartsWith("mytag", StringComparison.OrdinalIgnoreCase));
    step.InjectBefore($"My.Plugin.Manager.Mark(testRunner, {GeneratedCode.StringArray(hashtags)});");
}
`````

### Using them at runtime

The SDK only provides them at generation time. To make them available to step definitions, hooks and `[Scope]`, install the StepHashtags plugin, or pass the values to your own runtime code the way the example above does.

## Order

Every plugin has an order for generation and for the runtime chain. **For both, a higher order is closer to the step.** That is what makes plugins nest like parentheses instead of overwriting each other.

**Generation.** `GenerationInterceptor.Order` defaults to 1000 and interceptors are sorted ascending. "Before" hooks run in ascending order and "After" hooks in descending order, so a higher order ends up innermost:

`````text
Before: A (-100)  B (50)  C (100)
        A-open { B-open { C-open { ...steps... C-close } B-close } A-close
After:  C (100)  B (50)  A (-100)
`````

The same rule applies to per-step injections (`InjectBefore(code, order)`, `InjectAfter(code, order)`).

**Runtime chain.** `RegisterChainLink<TInterface, TBase, TLink>(order)` sorts links by order descending and wraps the base binding in that sequence, so the highest order sits next to the base (innermost) and the lowest is outermost. `int.MaxValue` is reserved. Avoid ties: equal orders are resolved by registration order, the later registered link being the outer one.

**Which container.** A chain lives in the container it is registered in, and its base is what the **parent** container resolves for `TInterface` (Reqnroll's default or any plugin's replacement); only a chain in a container without a parent starts from `TBase`. So register links **one level below the container that registers the service**. Reqnroll registers `IAsyncBindingInvoker` in the global container and resolves it only from the test-thread container (`TestExecutionEngine`, `StepArgumentTypeConverter`), so its links go into the test-thread container:

`````csharp
runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
    args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, MyBindingInvoker>(order: 0);
`````

That makes the chain independent of plugin load order: a plugin that is not built on the SDK and replaces the service in the global container (ReportPortal does, see [below](#plugins-not-built-on-this-sdk)) becomes the base of the chain instead of removing it or being removed. Each test-thread container builds its own chain.

A service that Reqnroll also consumes in the container that registers it has no level below: `IRuntimeBindingSourceProcessor` (binding discovery) is registered and used in the global container, so the custom-binding layer chains it there. Such a chain composes with other SDK links, but a plain replacement of the same service by another plugin still follows Reqnroll's rule: the last registration wins. BoDi offers no way around that, since a registration cannot be read back or changed once it has been resolved.

`RegisterChainLink` replaces a duplicate of the same link type, `RegisterChainLinkIfNotExists` keeps the first one, `AddChainLink` allows duplicates (`DuplicateBehavior`). `when:` makes a link conditional. A link class receives the inner instance in its constructor.

For reference, the defaults of the Ohirok plugins (all of them are configurable in the plugin's section):

| Plugin | `generationInterceptorOrder` | `bindingInvokerOrder` |
| --- | ---: | ---: |
| XFail | none (no generator interceptor) | 2000 |
| SoftAssert | 100 | -1000 |
| Retry | 50 | 0 |
| StepHashtags (IgnoreStep) | -1000000000 | 1000 |

StepHashtags is the outermost generation layer on purpose: it publishes a step's hashtags before any other plugin's injected code for that step runs, and clears them after.

**Per-scenario override.** A fluent interceptor that declares `TagPrefixes` accepts a per-scenario order through the plugin's own tag: `@mytag--order:30` makes the interceptor's order 30 for that scenario. It works for tags and hashtags.

Custom hooks (below) use a separate order: `Order` on the attribute, ascending, default 10000.

## Plugins on different SDK versions

Plugins from different authors are released at different times, so a project can have one plugin built on an older SDK next to others built on a newer one. Each package states the SDK version it needs, and NuGet picks for the project the lowest version that satisfies all of them: the version the newest plugin needs. That is the SDK the tests run with.

Reqnroll's generator needs the same, but it loads each generator plugin with the dependencies found next to it, and every SDK-based generator plugin carries its own copy of the SDK; the plugins must share one copy (the interceptor registry lives in it), so the generator uses the first one it loads. If that were an older plugin's copy, the newer plugins' features that the older SDK lacks would silently not run (for example [Variants](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Variants/README.md), which needs `TransformDocument`). So the SDK package adds its own assembly, the version NuGet picked, as the first generator plugin (a no-op one) through its `buildTransitive` targets, which apply whether the SDK is referenced directly or arrives through a plugin. The older plugin then runs on the newer SDK, which keeps what it uses.

For plugin authors this means:

- **Depend on the SDK package**, also for a generator-only package: it is what makes NuGet choose an SDK at least as new as yours and what brings the targets above.
- **Keep the SDK compatible for older plugins.** Additions are fine (`TransformDocument` came with a default in `GenerationInterceptor`); removing or changing what plugins already use would break those that are not rebuilt.

`tests/PackagingSmokeTests/MixedSdkNUnitSmoke` checks this with Catchy.ReqnrollPlugin 0.0.5, built on SDK 0.0.0, loaded before the Ohirok plugins.

## Plugins not built on this SDK

The SDK solves conflicts between plugins that use it. It cannot change how other plugins register themselves: a plugin that replaces a Reqnroll service with a plain `RegisterTypeAs` or `RegisterInstanceAs` still replaces it, and in the classic Reqnroll model the last registration wins.

**Cooperation with one such plugin is possible; between several of them it is not.** A chain has exactly one base. One plugin not built on the SDK that replaces, say, `IAsyncBindingInvoker` becomes that base, and every SDK plugin wraps it (ReportPortal below works this way). Two such plugins replacing the same service still exclude each other, however well each of them works with the SDK plugins on its own, because Reqnroll keeps only the last registration. The only thing that removes that conflict is moving the plugins onto this SDK: a port by their authors, a fork, or an SDK-based alternative (the Ohirok collection provides some, for example Retry and Variants). That is what this SDK is for: plugins that use it do not conflict, and that guarantee covers exactly the plugins that use it.

The SDK does what is possible from its own side:

- **Runtime.** A chain registered one level below the service's container (see [Which container](#order)) takes another plugin's replacement as its base. A replacement made in the *same* container as the chain still wins or loses by registration order.
- **Generation.** The interceptor engine takes over Reqnroll's feature generator: it registers `IFeatureGeneratorProvider` under the name `"default"` and `IFeatureGenerator`. Every interceptor of every SDK plugin runs inside it, but another plugin that replaces the same two services excludes it, or is excluded, depending on load order. Plugins that customize generation through other services (`IUnitTestGeneratorProvider`, `ITestGenerator`, Reqnroll's tag decorators) combine with it.

### Plugins we checked

Checked on 2026-11-28 against the Ohirok plugins (Retry, XFail, SoftAssert, StepHashtags), by decompiling each package's registrations and, where marked, by building and running a project with both. Downloads are NuGet totals on that date.

| Plugin (version, downloads) | What it replaces | With the Ohirok plugins | What to do |
| --- | --- | --- | --- |
| ReportPortal.Reqnroll 1.6.0, 1.2M | `IAsyncBindingInvoker` → `SafeBindingInvoker` (global container), only when enabled | Works: the Ohirok chain wraps `SafeBindingInvoker` (run, [compatibility test](https://github.com/olesme/ohirok.reqnroll/tree/main/tests/CompatibilityTests/ReportPortalNUnitTests)). Before SDK 0.0.1 the Ohirok runtime links were silently dropped | Nothing. See [Reporting tools](#reporting-tools-not-built-on-cucumber-messages) for what the agent shows |
| VariantsPlugin 2.0.5, 198k | `IFeatureGeneratorProvider "default"` and `IUnitTestGeneratorProvider`, always | **Conflict**: one feature generator excludes the other; in our run the Ohirok one won and `@Operator:Alpha @Operator:Beta` produced one test instead of two (run). Without Ohirok it also writes wrong Cucumber messages when a variant scenario is not the last one: each variant takes the next scenario's pickle (run) | Replace it with [Ohirok.Variants](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Variants/README.md): same tag syntax, correct messages, every runner |
| NUnitRetry.ReqnrollPlugin 2.1.0, 703k | `IUnitTestGeneratorProvider` (subclass of the NUnit provider) | Works: its `[NRetry]` and the Ohirok generated code are both in the test (run) | Use one retry plugin per scenario: its `@Retry(N)` and Ohirok's `@retry:N` are separate |
| xRetry.Reqnroll 1.1.0, 603k; xRetry.v3.Reqnroll 1.0.0, 12k | `IUnitTestGeneratorProvider` | Same mechanism as NUnitRetry, so it combines (not run) | As NUnitRetry |
| Reqnroll.ExternalData 3.3.4, 443k | `ITestGenerator` (subclass of `TestGenerator`) | Works: rows from the CSV and the Ohirok code are both generated (run) | Nothing |
| databinding.Reqnroll.NUnit.Retry 7.2.4, 82k; SpecSync TestSuiteBasedExecution 5.6.0, 114k | Nothing: they add named Reqnroll tag decorators (`ITestMethodDecorator`, `ITestClassDecorator`) | No shared service | Nothing |
| Allure.Reqnroll 2.15.0, 2.2M | `ITestRunner` (test-thread container) | No shared service | Nothing |
| Reqnroll.Microsoft.Extensions.DependencyInjection 3.3.4, 3.2M; Reqnroll.Autofac 3.3.4, 564k; Reqnroll.Windsor 3.3.4, 11k | `ITestObjectResolver` | No shared service | Nothing |
| Kronikol.ReqNRoll, TestTrackingDiagrams.ReqNRoll 2.37.4 | Wraps `IReqnrollOutputHelper` (test-thread container) | No shared service | Nothing |
| Reqnroll.Verify, Reqnroll.Assist.Dynamic, LivingDoc ExecutionDataCollector, Expressium.LivingDoc | Their own services, or a named `ICucumberMessageFormatter` | No shared service | Nothing |

Two plugins that replace the same service, neither of them on the SDK (for example VariantsPlugin and xRetry, both replacing `IUnitTestGeneratorProvider`), conflict with each other whether or not Ohirok is installed.

### Porting a plugin onto the SDK

| The plugin does this | On the SDK |
| --- | --- |
| Replaces a runtime service to wrap it (a binding invoker, an output helper, a test runner) | A chain link on that service, registered one level below the container Reqnroll registers it in |
| Changes the generated code of steps, scenarios or the test class | A generator interceptor |
| Adds attributes to generated test classes or methods per tag | Reqnroll's own tag decorators (`ITestMethodDecorator`, `ITestClassDecorator`), which already combine |
| Rewrites the feature before generation (VariantsPlugin, Reqnroll.ExternalData) | `GenerationInterceptor.TransformDocument`: code and Cucumber messages are both built from the result |
| Replaces the whole feature generator for anything else | Not supported. Open an issue: a missing generator hook is added to the SDK rather than worked around |

### Chain links at generation time

`RegisterChainLink` only uses BoDi, and Reqnroll's generator container is a BoDi container too, so a link registered from a generator plugin does build: two plugins linking `ITestGenerator` both apply, in order. It is not meant for generation and is not guaranteed not to break it, for three reasons:

- the generator container is a single container, so there is no level below to take another plugin's replacement as the base, and a plain replacement of the same service is dropped by the chain;
- the services that matter are named registrations that Reqnroll resolves all at once (`IFeatureGeneratorProvider` through `ResolveAll`) or resolves before plugins customize the container (`IUnitTestGeneratorProvider`): a link on the first adds a provider instead of wrapping one, and a link on the second needs the runner's concrete provider as `TBase`;
- the generator's classes are wired to each other by concrete types, so wrapping one of them can silently bypass the others.

For generation use generator interceptors for code and Reqnroll's tag decorators for attributes. If they are not enough, open an issue.

### Reporting tools not built on Cucumber Messages

The Ohirok plugins report through Reqnroll's standard channels: the step and scenario status, `IReqnrollOutputHelper` and attachments, which Reqnroll turns into Cucumber Messages. A reporter that reads those channels (Reqnroll's HTML formatter, the Ohirok report, any `ICucumberMessageFormatter`) sees what every plugin did without knowing about it. The ReportPortal agent (ReportPortal.Reqnroll) collects through its own hooks and reads only the final `ScenarioExecutionStatus` and `TestError`: it shows the outcome the Ohirok plugins produced (an expected failure as passed, a retried scenario by its final result), but not their output (warnings, issues, XFail and Retry notes). Making the agent read the output or the Cucumber Messages would close that for every plugin at once; that is a change for the agent, not for the plugins.

Until then, nothing has to be built per plugin: every event an Ohirok plugin reports also has a lifecycle hook, so a few lines in your own test project send it wherever you want, ReportPortal included. `ReportPortal.Shared` (a dependency of ReportPortal.Reqnroll) logs to the test that is running:

`````csharp
using Ohirok.Retry.Attributes;
using Ohirok.Retry.Core;
using Ohirok.SoftAssert.Attributes;
using Ohirok.SoftAssert.Core;
using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;
using ReportPortal.Shared;
using Reqnroll;

[Binding]
public class OhirokToReportPortal
{
    [OnRetry]
    public void Retried(RetryContext retry) =>
        Context.Current.Log.Warn($"Retry {retry.Type}: attempt {retry.Attempt} failed: {retry.LastException?.Message}");

    [AfterRetryLoop]
    public void RetryFinished(RetryContext retry)
    {
        foreach (var hookFailure in retry.HookFailures) Context.Current.Log.Warn(hookFailure);
        Context.Current.Log.Info($"Retry {retry.Type} {retry.Status} after {retry.Attempt} attempts"
            + (retry.IsFailed ? $": {retry.LastException?.Message}" : ""));
    }

    [OnXFail, OnAFail]
    public void ExpectedFailure(XFailScenarioContext xfail) =>
        Context.Current.Log.Warn(xfail.Failed
            ? $"{xfail.Mode} failure: {xfail.Exception!.GetType().Name}: {xfail.Exception.Message}"
            : $"{xfail.Mode}: no failure happened");

    [OnSoftFailure]
    public void SoftFailure(SoftAssertContext soft) => Context.Current.Log.Error($"Soft failure: {soft.Error?.Message}");
}
`````

What the hooks cover:

| Plugin | Reported to the output | Hook that sees it |
| --- | --- | --- |
| Retry | each failed attempt, the decision, the result, a timeout with its last error, `AfterScenario` failures of retried attempts | `[OnRetry]`, `[OnRetryDecision]`, `[AfterRetryLoop]` (`Status`, `LastException`, whose message names the last error after a timeout, `HookFailures`) |
| XFail | an expected or allowed failure that happened, an allowed failure that did not | `[OnXFail]`, `[OnAFail]` (`Exception` is null when no failure happened). A violated expectation (wrong exception, unexpected pass of `#xfail`) fails the test, so the agent sees it as a failure with its message |
| SoftAssert | each soft failure, the aggregate at the flush | `[OnSoftFailure]`, `[OnSoftFlush]` |
| StepHashtags | a step skipped by `#ignore` | No hook needed: the step is reported through Reqnroll's `ISkippedStepHandler`, which the ReportPortal agent implements |

The hook attributes take the same filters as the plugins' other hooks (tags, hashtags, retry type); see each plugin's README.

## Custom bindings and hooks

`CustomBindingAttribute` and `CustomHookAttribute` let plugins define their own attribute-driven hooks without involving Reqnroll's binding pipeline:

`````csharp
public sealed class OnSoftFlushAttribute : CustomHookAttribute
{
    public OnSoftFlushAttribute(params string[] filters) : base(filters) { Event = "SoftAssertFlush"; }
}
`````

Enable discovery in the runtime plugin with `CustomBindingsBootstrap.Setup(runtimePluginEvents)`, then fire the event yourself:

`````csharp
var hooks = provider.GetHooksForEvent("SoftAssertFlush");   // ICustomBindingProvider, ordered by Order
`````

**Important:** a custom hook does nothing by itself. Your plugin has to look it up and invoke it at the right moment.

**Filters.** Each hook carries string filters, evaluated by `FilterHelper.ShouldExecute(filters, states)` against the states your plugin builds for the current event (tags, hashtags, exception names, result…). A hook runs when at least one positive filter matches (or there are none) and no `!`-prefixed filter matches. Matching is case-insensitive and supports `*` and `?`.

Reference implementations: `CleanupAttribute` (PersistentCleanup), `OnXFailAttribute` / `OnAFailAttribute` (XFail), the retry hooks (Retry) and `OnSoftFailure` / `OnSoftFlush` (SoftAssert).

## Configuration

`PluginConfigProvider` reads the project's `reqnroll.json` and gives each plugin a typed section:

`````csharp
internal class MySection
{
    [JsonPropertyName("bindingInvokerOrder")]
    public int BindingInvokerOrder { get; set; } = 0;
}

var section = PluginConfigProvider.GetSection<MySection>("myplugin");   // defaults when absent
var flag = PluginConfigProvider.GetValue("allowCustomBindings", true);    // a single value by path
`````

Property names are matched case-insensitively; comments and trailing commas are allowed; enums are written as strings. Initialize the provider in each plugin: `CustomBindingsBootstrap.Setup(runtimePluginEvents)` does it for the runtime, and `PluginConfigProvider.Initialize(generatorPluginEvents)` is required in a generator plugin. Calling it from several plugins is safe.

**Don't keep a section in a static field of your own.** The provider reloads `reqnroll.json` for every project the generator builds and caches each section until the next reload, so call `GetSection` whenever you need the values. Reqnroll's generator can build several projects in one process (Visual Studio's MSBuild keeps the plugin assemblies loaded from one project to the next), and a static copy would hand the first project's settings to all the others.

Settings every plugin should expose when it has a position in a chain: `generationInterceptorOrder` and `bindingInvokerOrder`.

## Reporting signals

Plugins and step code can flag a scenario that passed but deserves attention, and embed rich content, without depending on any reporting plugin. The API is a set of extension methods on `IReqnrollOutputHelper` in `Ohirok.PluginChains.Common.Reporting`:

`````csharp
output.ReportIssue(ReportIssueCategory.RetrySucceeded, "Ohirok.Retry", "Passed after 3 attempts", tags: tags);
output.EmbedImage(screenshotBytes);
output.EmbedHtml("<a href=\"https://example.com/bug/1337\">Known bug #1337</a>", expanded: true);
`````

The calls write marker tags (`<ohirok-issue>`, `<ohirok-embed>`) to `IReqnrollOutputHelper`. With no Ohirok reporter installed they are plain output text and change nothing.

### Issues

| Method | What it writes |
| --- | --- |
| `ReportIssue(ReportIssue issue)` | An `<ohirok-issue>` tag with the issue's category, source plugin, severity, tags, hashtags and message. |
| `ReportIssue(category, sourcePlugin, message, tags = null, hashtags = null, severity = null)` | The same, from individual values. |

Categories (`ReportIssueCategory`): `RetrySucceeded`, `SoftAssertUnflushed`, `XFailPassed`, `StepSkipped`, `Custom`, `RetryTimedOut`, `ExpectedFailure`. The message is HTML-escaped.

### Embeds

| Method | Embed kind | Notes |
| --- | --- | --- |
| `EmbedImage(string filePath, bool? expanded = null)` | `image-data` | Reads the file and embeds it as a base64 `data:` URL; the media type comes from the extension. A path that is empty or does not exist is passed to `AddAttachment` instead. |
| `EmbedImage(byte[] bytes, string fileExtension = ".png", bool? expanded = null, string? mediaType = null)` | `image-data` | Embeds the bytes as a base64 `data:` URL. `mediaType` overrides the type derived from `fileExtension`. |
| `EmbedImageUrl(string url, string? altText = null, bool? expanded = null)` | `image-url` | References an image by URL; nothing is downloaded or copied. |
| `EmbedHtml(string rawHtml, bool? expanded = null)` | `raw-html` | Rendered verbatim, never HTML-encoded. Do not pass untrusted text. |
| `EmbedInfo(string message, bool? expanded = null)` | `callout`, `level="info"` | An informational callout. |
| `EmbedWarning(string message, bool? expanded = null)` | `callout`, `level="warning"` | A warning callout. |
| `EmbedError(string message, bool? expanded = null)` | `callout`, `level="error"` | An error callout. It does not fail the scenario. |

`expanded: true` shows the embed open in the report, `expanded: false` keeps it folded behind the step's "N attachments" disclosure, and leaving it out lets the report decide by kind (`reporting.expandAttachments`; by default callouts are open and everything else is folded). The callout message is written as HTML content, like `EmbedHtml`.

### Raw markers

`ReportMarkers` builds the tags directly, for an embed or an attribute the typed methods do not cover:

`````csharp
output.WriteLine(ReportMarkers.WrapEmbed(
    "image-url",
    [("expanded", "true"), ("url", "https://example.com/diagram.png"), ("alt", "Diagram")]));
`````

`WrapEmbed(kind, attributes, innerContent = "")` writes `<ohirok-embed kind="..." ...>innerContent</ohirok-embed>`. Attribute values are escaped, attributes with an empty or null value are omitted, and `innerContent` is written verbatim. `WrapIssue(category, source, severity, tags, hashtags, message)` builds the `<ohirok-issue>` tag that `ReportIssue` writes. The tag names are available as `ReportMarkers.EmbedTag` and `ReportMarkers.IssueTag`.

Written by hand, an embed looks like this:

`````html
<ohirok-embed kind="raw-html" expanded="true"><b>Deployed build 42</b></ohirok-embed>
<ohirok-embed kind="callout" level="warning">Slow response from the payment sandbox</ohirok-embed>
<ohirok-embed kind="image-url" url="https://example.com/diagram.png" alt="Diagram"></ohirok-embed>
`````

The kinds the SDK writes are `image-data` (attributes `data`, `alt`), `image-url` (`url`, `alt`), `callout` (`level`: `info`, `warning` or `error`) and `raw-html`; `expanded="true"` or `expanded="false"` can be added to any of them.

### Links to bugs and backlog items

The report does not turn tags into tracker links by itself; a hook in your project does it with `EmbedHtml`. It reads the scenario's tags (for example `@bug:1337`, `@pbi:4821`), builds the URLs for your tracker, and embeds them:

`````csharp
[Binding]
public class TrackerLinkHooks(ScenarioContext scenario, IReqnrollOutputHelper output)
{
    [AfterScenario]
    public void LinkWorkItems()
    {
        var links = scenario.ScenarioInfo.Tags
            .Where(t => t.StartsWith("bug:", StringComparison.OrdinalIgnoreCase))
            .Select(t => $"<a href=\"https://github.com/your-org/your-repo/issues/{WebUtility.HtmlEncode(t[4..])}\">Known bug #{WebUtility.HtmlEncode(t[4..])}</a>");
        if (links.Any())
            output.EmbedHtml(string.Join(" &middot; ", links), expanded: true);
    }
}
`````

The complete version, with backlog items and a "report a bug" link that appears only when the scenario failed, is [`examples/ReportShowcase/Showcase.Steps/EvidenceSteps.cs`](https://github.com/olesme/ohirok.reqnroll/blob/main/examples/ReportShowcase/Showcase.Steps/EvidenceSteps.cs).

### Writing after the last step

Reqnroll's message formatter accepts output only while a step or a hook is current. Output written from plain test-body code, or after the last step, is dropped silently. When a plugin reaches its verdict there (a retry loop, an expected failure, any wrapper around the test method), queue the write with `DeferredOutput`:

`````csharp
DeferredOutput.Enqueue(scenarioContext, output =>
    output.ReportIssue(ReportIssueCategory.RetrySucceeded, "MyPlugin", "Passed after 2 attempts"));
`````

The queued writes run in an `AfterScenario` hook with order `int.MaxValue` (after your own hooks), with the scenario's output helper. The SDK registers that hook once, from `CustomBindingsBootstrap.Setup`, so a plugin needs no `[Binding]` class or step-assembly entry of its own. A binding-invoker chain link that tracks hooks (retries, timeouts) can let the flush through untouched with `DeferredOutput.IsFlushHook(binding)`. Retry and XFail write their scenario-level signals this way.

## Other helpers

- `TagValueParser.TryExtractValues` pulls values out of tags such as `xfail(Ex)`, `afail[Ex1,Ex2]`, `expected-fail:Ex1;Ex2`.
- `ContainerExtensions.TryResolve<T>` resolves from a BoDi container if registered.
- `StorageReplaceMode` and `LateBoundStorageProxy<T>` implement late-bound storage backends shared by PersistentCleanup and Reporting.

## See also

- [StepHashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md): runtime access to step hashtags, `#ignore`.
- [Retry](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Retry/README.md): retry engine and its lifecycle hooks.
- [SoftAssert](https://github.com/olesme/ohirok.reqnroll/blob/main/src/SoftAssert/README.md): soft-assertion capture and flush.
- [XFail](https://github.com/olesme/ohirok.reqnroll/blob/main/src/XFail/README.md): expected and allowed failures, built on the chain and custom-binding layers.
- [Variants](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Variants/README.md): one test per variant, built on `TransformDocument`.
- [PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md): `CustomBindingAttribute` usage in cleanup registration.
- [Reporting](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md): the report layer that consumes the reporting signals.
