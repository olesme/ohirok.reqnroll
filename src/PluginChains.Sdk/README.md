# Ohirok.Reqnroll.PluginChains.Sdk

An SDK for building Reqnroll plugins that cooperate instead of replacing each other.

Reqnroll lets a plugin take over a runtime or generator extension point, but not share it. This SDK adds the missing composition layer: chained generator interceptors and runtime wrappers with a defined order, attribute-driven custom hooks, shared configuration, and report signals. It does not depend on any particular plugin.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md): this SDK and a set of Reqnroll plugins built on it (StepHashtags, XFail, Retry, SoftAssert, PersistentCleanup, Reporting). Those plugins are complete tools you can use on their own, and their source is a working reference for writing plugins of your own; the examples below point to them.

Use the SDK when you need to:

- wrap a generator hook or runtime invoker without the last plugin overwriting the previous one;
- register multiple interceptors in a predictable, configurable order;
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
    PluginConfigProvider.Initialize(events);   // required to read reqnroll.json in the generator

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

`````csharp
runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
    args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, MyBindingInvoker>(order: 0);
`````

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

`PluginConfigProvider` reads `reqnroll.json` once and gives each plugin a typed section:

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

Categories (`ReportIssueCategory`): `RetrySucceeded`, `SoftAssertUnflushed`, `XFailPassed`, `StepSkipped`, `Custom`, `RetryTimedOut`. The message is HTML-escaped.

### Embeds

| Method | Embed kind | Notes |
| --- | --- | --- |
| `EmbedImage(string filePath, bool expanded = false)` | `image-data` | Reads the file and embeds it as a base64 `data:` URL; the media type comes from the extension. A path that is empty or does not exist is passed to `AddAttachment` instead. |
| `EmbedImage(byte[] bytes, string fileExtension = ".png", bool expanded = false, string? mediaType = null)` | `image-data` | Embeds the bytes as a base64 `data:` URL. `mediaType` overrides the type derived from `fileExtension`. |
| `EmbedImageUrl(string url, string? altText = null, bool expanded = false)` | `image-url` | References an image by URL; nothing is downloaded or copied. |
| `EmbedHtml(string rawHtml, bool expanded = false)` | `raw-html` | Rendered verbatim, never HTML-encoded. Do not pass untrusted text. |
| `EmbedInfo(string message)` | `callout`, `level="info"` | An informational callout. |
| `EmbedWarning(string message)` | `callout`, `level="warning"` | A warning callout. |
| `EmbedError(string message)` | `callout`, `level="error"` | An error callout. It does not fail the scenario. |

`expanded: true` makes the report open the step's attachments block by default instead of leaving it collapsed. The callout message is written as HTML content, like `EmbedHtml`.

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

The kinds the SDK writes are `image-data` (attributes `data`, `alt`), `image-url` (`url`, `alt`), `callout` (`level`: `info`, `warning` or `error`) and `raw-html`; `expanded="true"` can be added to any of them.

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

> **Where you may write.** Reqnroll's message formatter accepts output only while a step or a hook is current. Output written from plain test-body code, or after the last step, is dropped silently. Write from a step or a hook, or queue the write and flush it from a hook (Retry does this in an `AfterScenario` hook).

## Other helpers

- `TagValueParser.TryExtractValues` pulls values out of tags such as `xfail(Ex)`, `afail[Ex1,Ex2]`, `expected-fail:Ex1;Ex2`.
- `ContainerExtensions.TryResolve<T>` resolves from a BoDi container if registered.
- `StorageReplaceMode` and `LateBoundStorageProxy<T>` implement late-bound storage backends shared by PersistentCleanup and Reporting.

## See also

- [StepHashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md): runtime access to step hashtags, `#ignore`.
- [Retry](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Retry/README.md): retry engine and its lifecycle hooks.
- [SoftAssert](https://github.com/olesme/ohirok.reqnroll/blob/main/src/SoftAssert/README.md): soft-assertion capture and flush.
- [XFail](https://github.com/olesme/ohirok.reqnroll/blob/main/src/XFail/README.md): expected and allowed failures, built on the chain and custom-binding layers.
- [PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md): `CustomBindingAttribute` usage in cleanup registration.
- [Reporting](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md): the report layer that consumes the reporting signals.
