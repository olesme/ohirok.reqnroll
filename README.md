# Ohirok Project

![Ohirok Project](assets/ohirok_project.svg)

[![NuGet](https://img.shields.io/nuget/v/Ohirok.Reqnroll.PluginChains.Sdk.svg)](https://www.nuget.org/packages/Ohirok.Reqnroll.PluginChains.Sdk)
[![CI](https://github.com/olesme/ohirok.reqnroll/actions/workflows/ci.yml/badge.svg)](https://github.com/olesme/ohirok.reqnroll/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

**Ohirok Project** started as my attempt to bring a few of my own ideas into the .NET BDD world.

`Ohirok.Reqnroll` is an SDK together with a set of plugins built on top of it. It allows independent plugins to participate in the same extension point instead of replacing one another — for example, several plugins can now modify the same Binding Invoker without having to know about each other — and it makes it possible to inject custom code into almost any part of a generated test class during generation.

## Quick links

- [Plugin Chains SDK](src/PluginChains.Sdk/README.md): SDK for building Reqnroll plugins that cooperate without conflict

Plugins built on it:

- [StepHashtags](src/StepHashtags/README.md): per-step metadata in comments, `#ignore`
- [Retry](src/Retry/README.md): scenario, step, and block retry engine with hooks and state handling
- [XFail](src/XFail/README.md): expected and allowed failure semantics for individual steps
- [SoftAssert](src/SoftAssert/README.md): soft assertions that aggregate failures without aborting the scenario
- [Variants](src/Variants/README.md): one test per variant (`@Browser:Chrome @Browser:Firefox`)
- [AutoTags](src/AutoTags/README.md): tags and step hashtags from step texts (a mock-server step gets `@MockServer`, payment steps `#retry:2`)
- [IgnoreReasons](src/IgnoreReasons/README.md): your own ignore tags with reasons (`@ignore:bug` → *Known product defect* in the test results)
- [PersistentCleanup](src/PersistentCleanup/README.md): durable cleanup registry with database and custom storage backends
- [Reporting](src/Reporting/README.md): standalone HTML reporting, embeds, history, and report templates

## Why I built this

I'm a big fan of Reqnroll and genuinely grateful to its authors for their work. One of its greatest strengths is extensibility: nearly every stage of Reqnroll, from code generation to runtime execution, can be customized or completely replaced simply by swapping services in the BoDi container.

What I felt was missing wasn't extensibility itself, but a way for extensions to cooperate.

If two plugins replace the same component, the last one registered simply overwrites the first — you don't even find out about the other one. To use several plugins together, you end up writing monstrous plugin packs that combine every participant together.

For runtime plugins, the SDK introduces a middleware pipeline. Each plugin does its own work and delegates to the next one. The important part: every plugin can be built, tested and shipped independently — it doesn't need to know who else is in the pipeline, or even whether it's the only one installed.

The generation stage has its own limits. Decorators are great for attaching attributes to generated test methods. Once you need to actually change the generated code, things become much harder. The usual solution is to replace the Unit Test Provider. That means maintaining separate implementations for NUnit, xUnit (v2 and v3), MSTest and TUnit, even if your transformation has nothing to do with the test framework itself. It also means rebuilding code that has already been generated, which isn't particularly elegant or efficient.
I decided to approach the problem from a different angle.

For code generation, instead of rebuilding code after it has already been generated, the SDK weaves its changes during generation itself. This avoids an extra transformation step and keeps the customization independent of the underlying unit test provider. Internally, this is done by patching the Feature Generator Provider rather than replacing individual Unit Test Providers. A plugin can also rewrite the parsed feature before anything is generated (Variants turns tagged scenarios into Scenario Outline examples this way), and since the Cucumber messages are built from the same rewritten feature, tests and reports always agree.

On top of that, the SDK provides a registry for custom bindings — mostly hooks — that look and behave like the built-in ones, while staying discoverable by other plugins.

All of this holds between plugins built on the SDK. A plugin that is not, and replaces the same service, still follows Reqnroll's rule: one such plugin can sit underneath the SDK plugins (ReportPortal's agent does, and is covered by a compatibility test), but two of them replacing the same service still exclude each other. Moving them onto the SDK is what removes that. The [SDK README](src/PluginChains.Sdk/README.md#plugins-not-built-on-this-sdk) lists the popular plugins that were checked and what to do about each.

## Plugins built on top of the SDK

The SDK itself is intended for plugin authors. The plugins below are built on it, work independently of each other (installing one never requires another), and double as reference implementations.

### Retry

Unlike most retry implementations, Ohirok.Retry isn't limited to rerunning whole scenarios. It supports retries at multiple levels:

- retries whole scenarios and outlines, but also individual steps and groups of steps;
- can be restricted to specific exception types or custom conditions;
- makes the retry state available through the context, the test output, and Cucumber Messages;
- provides lifecycle hooks — before the retry loop starts, when a new attempt begins, after the last attempt;
- named policies: `@retry:payments` on the scenario, its settings in one hook ([Named policies](src/Retry/README.md#named-policies));
- behaves the same way across NUnit, TUnit, xUnit (v2 and v3), and MSTest;
- says what its number counts: `@retry:3` is three retries after the first run, `@attempts:3` is three runs in all, `@noretry` switches it off.

```gherkin
@retry:3
Scenario: Unstable scenario
    When the flaky thing happens

Scenario: Retry of a single step
    #retry:3 #condition:TimeoutException
    When the network call might time out
    Then the result is valid

Scenario: Retry of a block of steps
    #retryblock:3
    When step one might fail
    And step two might fail
    #end
    Then the final assertion holds
```

During execution, RetryContext gives hooks and step definitions access to the current attempt number, elapsed time, the last exception, the configured conditions, and the tags. Like other Reqnroll contexts, it also carries a shared data dictionary.

### PersistentCleanup

PersistentCleanup is for resources that need to be cleaned up eventually, even if a test fails or the whole test run gets interrupted. Cleanup actions are registered in a persistent store and can run immediately or automatically before the next test run.

This solves two common problems:

- the test failed — you get a chance to do a manual post-mortem on the very same data;
- the whole run died — in which case the cleanup may never have been reached at all, but it will simply run before the next test starts.

The registry requires some form of storage (a database, etc.). Ready-made implementations are provided for several databases, and you can always implement your own ICleanupStorage.

```csharp
[Cleanup]
public Task DeleteUser(string userId) => _userService.DeleteAsync(userId);

await _cleanupService.AddAsync<MyCleanups>(
    c => c.DeleteUser("user123"),
    description: "Remove test user");

// Run it explicitly, or let the plugin run it before the next test
await _cleanupService.PerformAllAsync();
```

Ready-made storages: SQLite, PostgreSQL, SQL Server, MySQL, MongoDB, Cosmos DB, Redis. Anything else — through `ICleanupStorage`.

### StepHashtags

StepHashtags allow tag-like metadata to be attached to individual steps, written as ordinary Gherkin comments.

```gherkin
Scenario: External call with metadata
    #timeout:5000 #priority
    When I call the external service
    Then the response is valid
```

The `#…` syntax is parsed by the SDK, so plugins such as Retry (`#retry`, `#retryblock`), XFail and SoftAssert read their hashtags without installing this package. StepHashtags is the optional runtime layer: step definitions and hooks read the current step's hashtags through `GetStepHashtags()` / `HasStepHashtag()`, and the built-in `#ignore` skips a step.

Step-level hashtags are fully compatible with Reqnroll's scoped bindings. Traditionally, if you wanted a different binding implementation for a single step, you had to tag the entire scenario, even though the tag only affected one step. With step-level hashtags, the intent is attached directly to the step itself, making it obvious what is being customized while allowing different steps in the same scenario to be handled differently.

### XFail

Support for expected (XFail) and allowed (AFail) failures.

The idea behind XFail comes from the Python testing ecosystem, where expected failures are a well-established concept.
I found it useful in Reqnroll as well, especially for expressing temporary or conditional behaviour without rewriting step implementations.
In .NET, the usual approach is to rewrite the test so that it explicitly expects an exception. That works well when the exception is the behaviour you're testing. It is less convenient when the exception is only a temporary condition you want to acknowledge without changing the step itself.
With #xfail, the expectation stays at the feature level. When the known bug is fixed and the exception disappears, the test starts failing, reminding you to verify whether the feature now behaves as intended and to remove the temporary expectation.
XFail also turned out to be extremely useful for testing the plugins themselves. When a plugin is supposed to alter execution by throwing, swallowing or transforming exceptions, the expected failure becomes part of the assertion instead of making the test suite fail.
AFail has fewer use cases, but it can be useful when a known problem shouldn't fail the test because the fallback behaviour is still worth exercising. For example, a third-party request may occasionally time out. The timeout is expected and shouldn't fail the scenario, but the test should still verify that logging, retries or fallback behaviour worked correctly.

```gherkin
Scenario: Known bug in validation
    Given I have invalid input
    #xfail
    When I submit the form
    Then the error is logged

Scenario: Unstable third-party call
    #afail:IOException
    When I call the external API
    Then the fallback ran
```

`#xfail` — the step must fail (with the given exception type, if one is specified); if it passes, the test fails.
`#afail` — the step may fail (the failure is swallowed) or may pass; either is fine.

Type patterns support wildcards (`*Exception`) and exclusions (`#xfail:*Exception,!TimeoutException`). Alternative spellings: `#expect-fail`, `#allow-fail`.

### SoftAssert

Mark a step with `#softassert` and its failure is captured instead of aborting the scenario — execution continues to the next step, and the scenario still fails overall once the collected failures are reported at the end of the scenario. Built on [Catchy](https://www.nuget.org/packages/Catchy.ReqnrollPlugin)'s soft-assertion engine; this plugin just wires it to a tag instead of requiring `StatefulAsserter.Soft` calls in your step code.

```gherkin
Scenario: Multiple checks
    Given the page has loaded
    #softassert
    Then the header is correct
    #softassert
    Then the footer is correct
    Then the page is still usable
```

`[OnSoftFailure]`/`[OnSoftFlush]` hooks (the same custom-hook family the SDK registry provides to every plugin) let you react to each captured failure or to the final flush.

### Variants

Tag a scenario, a rule or a whole feature with `@Browser:Chrome @Browser:Firefox` (after naming `Browser` as a variant key in `reqnroll.json`) and every scenario it covers runs once per browser. Several keys give every combination. The feature is rewritten into Scenario Outline Examples before generation, so the variants are ordinary row tests on every runner, the value is available as `<Browser>` and in `ScenarioInfo.Arguments`, and the Cucumber messages match the tests one to one in every report.

```gherkin
@Browser:Chrome @Browser:Firefox
Scenario: Access the dashboard
    Given the user is logged in on <Browser>
    Then they see their account summary
```

### AutoTags

Map step texts to tags in `reqnroll.json` (`"autoTags": { "tags": { "@MockServer": ["^the mock server returns"] } }`) and every scenario using such a step, directly or in its Background, gets the tag. It is added before generation, like a tag written in the file: runner categories, `[BeforeScenario("MockServer")]` hooks, `ScenarioInfo.Tags` and the Cucumber messages all have it, so nobody has to remember the tag on scenario 4,000. Step hashtags work the same way: `"hashtags": { "#retry:2": ["^the payment gateway"] }` retries every payment-gateway step, as if `#retry:2` were written above it.

### IgnoreReasons

Name your own ignore tags with a reason (`"ignoreReasons": { "tags": { "@ignore:bug": "Known product defect" } }`). A scenario, rule or feature with such a tag is ignored exactly like `@ignore`, and every runner (NUnit, MSTest, xUnit, TUnit) shows the reason next to the skipped test.

### Reporting

A standalone, collapsible HTML living-doc report rendered from the same Cucumber Messages stream Reqnroll already produces — install `Ohirok.Reporting.ReqnrollPlugin` and point `reqnroll.json`'s `formatters.ohirokReport` at an output path. It is designed for practical troubleshooting and review:

- a real **"pass with issues"** status (yellow) for a scenario that passed only via retries, soft asserts, partial step skips, or expected/allowed failures — these would otherwise render as a plain, misleading green;
- **custom embeds** attached at any point during a step — `EmbedImage`, `EmbedInfo`, `EmbedWarning`, `EmbedHtml`, `EmbedError` — rendered inline (thumbnail + expand/download for images);
- a searchable, filterable, collapsible tree (folders → features → rules → outlines/examples → scenarios → retries/attempts), dark/light theme, and deep-linking via URL query params;
- **run history and health** (`New`/`Flaky`/`Regressed`/`Broken`/`Fixed`/`Passing`) across runs, backed by a pluggable `IReportHistoryStorage` — ready-made adapters for SQLite, PostgreSQL, SQL Server, MySQL, MongoDB, Cosmos DB, Redis, and a built-in append-only ndjson file-based backend (`Ohirok.Reporting.History.FileSystem.ReqnrollPlugin`) for CI pipelines that just need a restorable artifact between runs; `Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin` for anything else;
- living documentation of the feature files in place of SpecFlow+ LivingDoc: descriptions, rules, tables and Examples, with each scenario's latest result ([Living documentation](src/Reporting/README.md#living-documentation));
- `Ohirok.Reporting.Combine`, a standalone CLI tool, merges multiple runs' `.ndjson` output into one combined report — including chaining a base run with reruns of only its failed scenarios into one unified attempt history;
- templating overrides (`reporting.css` for a sync-safe CSS overlay, `template`/`js` for a full fork — each resolved either by a literal file path or by the name of a built-in theme, e.g. `"css": "ultraviolet"`) so the report's look can be restyled or rebuilt without forking the whole formatter — see `examples/ReportShowcase/`.

To evaluate the report, run [`examples/ReportShowcase/showcase.ps1`](examples/ReportShowcase/README.md): real runs of four test projects over several simulated builds give every report the stack produces - each theme, history, reruns and combined projects - with a landing page linking them all.

Each sub-package has its own README with installation/configuration details (`src/Reporting/*/README.md`).

## Examples

`examples/` has two runnable examples: the [report showcase](examples/ReportShowcase/README.md), four test projects on all four runners that install every plugin and produce every kind of report (themes, history, reruns, combined runs), and [PersistentCleanup](examples/PersistentCleanupExample/README.md) with its storage backends. See [`examples/README.md`](examples/README.md).

## Building your own plugins

The SDK exists for plugin authors, not just for the plugins in this repository. If you have an idea for another extension, I'd like to hear about it. And if the SDK is missing an extension point, a hook, or some other capability your plugin needs, that's worth an issue too — growing those extension points is very much part of the point of this project.

## Warning! Antipatterns detected

A couple of my plugins rely on BDD anti-patterns, mostly step-level hashtags. That said, I'd argue it’s not always a crime. I’ve put together a quick defense of this approach for specific use cases here:
[StepHashtags](src/StepHashtags/README.md)

I’d love to hear your thoughts on this as well.

## Contributing

Raise an issue, open a discussion, propose a PR, fork it, or just use it — I'll be glad either way. Build, test and packaging commands are in [CONTRIBUTING.md](CONTRIBUTING.md).

## Packages

| Package | Purpose |
| --- | --- |
| `Ohirok.Reqnroll.PluginChains.Sdk` | The SDK: generator interceptors, runtime chains, custom hooks, configuration, report signals |
| `Ohirok.StepHashtags.ReqnrollPlugin` | Runtime access to step hashtags, `#ignore` |
| `Ohirok.Retry.ReqnrollPlugin` | Scenario, step and block retries |
| `Ohirok.XFail.ReqnrollPlugin` | Expected and allowed step failures |
| `Ohirok.SoftAssert.ReqnrollPlugin` | Soft assertions on marked steps |
| `Ohirok.Variants.ReqnrollPlugin` | One test per variant (`@Browser:Chrome @Browser:Firefox`) |
| `Ohirok.AutoTags.ReqnrollPlugin` | Tags and step hashtags from step texts |
| `Ohirok.IgnoreReasons.ReqnrollPlugin` | Ignore tags with reasons shown by the test runner |
| `Ohirok.PersistentCleanup.Core` | Cleanup engine and storage contract (arrives with an adapter) |
| `Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin`, `Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin`, `Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin`, `Ohirok.PersistentCleanup.MySql.ReqnrollPlugin`, `Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin`, `Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin`, `Ohirok.PersistentCleanup.Redis.ReqnrollPlugin` | Cleanup storage adapters |
| `Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin` | Cleanup with your own `ICleanupStorage` |
| `Ohirok.Reporting.ReqnrollPlugin` | The `ohirokReport` formatter: standalone HTML report |
| `Ohirok.Reporting.Core` | Report model, parsing and rendering (arrives with the formatter) |
| `Ohirok.Reporting.Combine` | .NET tool that combines several runs into one report |
| `Ohirok.Reporting.History.Core` | Run-history contract and health analysis |
| `Ohirok.Reporting.History.Sqlite.ReqnrollPlugin`, `Ohirok.Reporting.History.FileSystem.ReqnrollPlugin`, `Ohirok.Reporting.History.PostgreSql.ReqnrollPlugin`, `Ohirok.Reporting.History.MsSql.ReqnrollPlugin`, `Ohirok.Reporting.History.MySql.ReqnrollPlugin`, `Ohirok.Reporting.History.MongoDb.ReqnrollPlugin`, `Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin`, `Ohirok.Reporting.History.Redis.ReqnrollPlugin` | Run-history storage backends |
| `Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin` | Run history with your own `IReportHistoryStorage` |

## License

MIT, see [LICENSE](LICENSE). The SDK contains code adapted from Reqnroll (BSD 3-Clause), and the report embeds a few third-party icons and fonts (ISC, SIL OFL 1.1); see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
