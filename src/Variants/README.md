# Ohirok.Variants.ReqnrollPlugin

Runs a scenario once per variant. Tag it `@Browser:Chrome @Browser:Firefox` and you get two tests, one per browser, on NUnit, MSTest, xUnit v3 and TUnit, with Cucumber messages that match the tests one to one, so every report (Reqnroll's HTML report, the [Ohirok report](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md), any formatter) shows each variant as its own test.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md); built on the [PluginChains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md), so it works together with the other plugins built on it.

## Installation

```sh
dotnet add package Ohirok.Variants.ReqnrollPlugin
```

Then name the tags that mark variants in `reqnroll.json`. Nothing is expanded until a key is configured, so other `name:value` tags (`@issue:123`) are never touched:

```json
{
  "variants": { "keys": [ "Browser" ] }
}
```

The package only changes how tests are generated: it adds nothing to your test assembly and nothing runs at test time.

## Usage

```gherkin
@Browser:Chrome @Browser:Firefox
Scenario: Access the dashboard
  Given the user is logged in on <Browser>
  When they open the dashboard
  Then they see their account summary
```

This generates two tests. Read the value like any Scenario Outline argument: as `<Browser>` in step text, or in code:

```csharp
[BeforeScenario]
public void StartBrowser(ScenarioContext scenarioContext)
{
    var browser = (string)scenarioContext.ScenarioInfo.Arguments["Browser"];
    // start the browser
}
```

### Where variants can be declared

| Tags on | Apply to |
| --- | --- |
| a scenario or Scenario Outline | that scenario |
| a `Rule` | every scenario in the rule |
| the `Feature` | every scenario in the file |

For each key the most specific level wins: a scenario's own `@Browser:` tags replace its rule's, and a rule's replace the feature's. Keys a level does not mention are inherited, so `@OS:Linux` on a scenario keeps the feature's `@Browser:` values and adds the OS.

**Several keys** give every combination: `@Browser:Chrome @Browser:Firefox @OS:Linux @OS:Windows` is four tests.

**A Scenario Outline** keeps its Examples: each Examples block runs once per variant, with the variant added as a column. If a block already has a column named like the key (`| Browser |`), that block's own values are used and the variant tags do not apply to it.

**Tags.** The variant tags are moved from the feature, rule and scenario to the generated Examples blocks, one block per combination, so every generated test carries only its own variant tags: filter a run with your test runner's category filter (`Browser:Chrome`) or by tag in a report.

A variant tag needs a value: `@Browser:` fails the build with the file and line.

## How it works

The plugin rewrites the parsed feature before anything is generated: a tagged scenario becomes a Scenario Outline with one Examples block per variant. Reqnroll then generates it through its own Scenario Outline support, so the tests are ordinary row tests on every runner, and the Cucumber messages (Gherkin document, pickles) are built from the same rewritten feature as the code. The `.feature` file itself is not changed.

This uses the SDK's document hook (`GenerationInterceptor.TransformDocument`), so it combines with other plugins built on the SDK, and with Reqnroll.ExternalData: a `@DataSource` scenario's rows are multiplied by the variants too (covered by `tests/CompatibilityTests/ExternalDataVariantsNUnitTests`).

**Putting the value in the title.** `<Browser>` in a scenario's name is replaced like in any Scenario Outline, so `Scenario: Search works in <Browser>` reports as *Search works in Chrome*, *Search works in Firefox*. The [report showcase](https://github.com/olesme/ohirok.reqnroll/blob/main/examples/ReportShowcase/README.md) has a cross-browser feature that shows how variants look in the reports.

## Configuration

```json
{
  "variants": {
    "keys": [ "Browser", "OS" ],
    "generationInterceptorOrder": 1000
  }
}
```

| Setting | Default | Meaning |
| --- | --- | --- |
| `keys` | none | Tag names that mark variants. Matched without regard to case; the generated column uses the spelling from this list. |
| `generationInterceptorOrder` | `1000` | Order among the document hooks of other SDK plugins (ascending, each sees the previous one's result). |

## Coming from VariantsPlugin

[VariantsPlugin](https://www.nuget.org/packages/VariantsPlugin) uses the same tag syntax, so feature files keep working:

| VariantsPlugin | Ohirok.Variants |
| --- | --- |
| `"variantkey": "Browser"` | `"variants": { "keys": [ "Browser" ] }` (several keys allowed) |
| `scenarioContext.TryGetValue("Browser", out var browser)` | `scenarioContext.ScenarioInfo.Arguments["Browser"]`, or `<Browser>` in the steps |
| variant tags on the feature or on scenarios, not both | feature, rule and scenario; the most specific level wins |
| NUnit and xUnit | NUnit, MSTest, xUnit v3, TUnit |
| `isRetryActive` (its own NUnit retry) | use a retry plugin, e.g. [Ohirok.Retry](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Retry/README.md) |

Remove VariantsPlugin when you add this package: both take over the generation of the same features, so only one of them can be installed.

## See also

- [PluginChains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md): `TransformDocument` and the other generator hooks.
- [Plugins not built on the SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md#plugins-not-built-on-this-sdk): what was checked and how conflicts are resolved.
