# Ohirok.AutoTags

Tags scenarios and steps by their step texts. Say which step texts need a tag or a step hashtag in `reqnroll.json`, and
every scenario using such a step gets the tag, and every such step the hashtag, so nobody has to remember to write them.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md) — a plugin-authoring SDK and a set of Reqnroll plugins.

## Why

Some steps need preparation that is scoped by a tag: a mock server, a seeded database, a browser. The hook is
`[BeforeScenario("MockServer")]`, and every scenario using a mock-server step must carry `@MockServer`. Out of
thousands of scenarios, one without it fails, and nothing in the feature file shows why. AutoTags derives the tag from
the steps instead.

## Installation

```bash
dotnet add package Ohirok.AutoTags.ReqnrollPlugin
```

A generator plugin: it runs at build time and adds nothing to the test output. It needs no code and no MSBuild setup.

## Configuration

```json
{
  "autoTags": {
    "tags": {
      "@MockServer": [ "^the mock server (returns|responds)", "^a stubbed .+ endpoint" ],
      "@Database": [ "in the database$" ]
    }
  }
}
```

Each key is a tag (the `@` is optional), each value a list of regular expressions matched against the step text
without its keyword, ignoring case. With the configuration above:

```gherkin
Scenario: Order total uses the current price
    Given the mock server returns the price 12.50
    When I add the product to the cart
    Then the total is 12.50
```

is generated, run and reported as if it were written with `@MockServer` above `Scenario:`, so it gets:

- the runner category (`[Category("MockServer")]` in NUnit, and the equivalent in MSTest, xUnit and TUnit), so
  `dotnet test --filter Category=MockServer` selects it;
- the hooks scoped by the tag, such as `[BeforeScenario("MockServer")]`;
- the tag in `ScenarioInfo.Tags` and in the Cucumber messages, so reports (the Ohirok report and Reqnroll's own HTML
  report) and tag filters show it.

## Step hashtags

The same works for [step hashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md), the
metadata the other plugins read from a comment above a step. A step whose text matches gets the hashtag:

```json
{
  "autoTags": {
    "hashtags": {
      "#retry:2": [ "^the payment gateway" ],
      "#softassert": [ "^the page shows" ]
    }
  }
}
```

Every step starting with *the payment gateway* is retried twice, as if `#retry:2` were written above it, and every
*the page shows ...* check is a soft assertion. Retry, XFail, SoftAssert and StepHashtags (`GetStepHashtags()`) see the
hashtag like a written one. The feature file and the Cucumber messages stay as written, so a report shows only the
hashtags written in comments.

A hashtag written above a step comes after the configured ones, so for a setting read as its last value (`#retry:3` above
the step and `#retry:2` from the configuration) the written one wins.

## Rules

- A scenario gets a tag when one of its own steps matches one of the tag's patterns, or a step of a `Background` that
  runs before it: the feature's for every scenario, a rule's for the scenarios of that rule.
- A tag the scenario, its rule or its feature already has is not added again.
- A Scenario Outline is matched on its step texts as written (with `<placeholders>`), and the tag covers every
  example.
- An invalid pattern stops the build with a message naming the pattern and its tag.
- A step gets a hashtag when its own text matches; a Background step's hashtags apply to that step wherever it runs.
- Patterns are compiled once per project, and each distinct step text is matched against them once per project.

| Key | Default | Meaning |
| --- | --- | --- |
| `autoTags.tags` | `{}` | Tag → step-text patterns. |
| `autoTags.hashtags` | `{}` | Step hashtag → step-text patterns. Nothing changes until a tag or a hashtag is configured. |
| `autoTags.generationInterceptorOrder` | `500` | Position among the plugins that rewrite the feature before generation (lower first); see the [SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md). Variants runs at `1000`, so its variants are made from scenarios that already have their tags. |

## See also

- [Variants](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Variants/README.md): the other plugin that rewrites the feature before generation.
- [Plugin Chains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md): `TransformDocument`, which this plugin is built on.
