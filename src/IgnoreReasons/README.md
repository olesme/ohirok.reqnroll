# Ohirok.IgnoreReasons

Ignore tags with reasons. Name your own tags, such as `@ignore:bug` or `@wip`, give each a reason in `reqnroll.json`,
and the scenarios they cover are ignored like `@ignore`, with that reason shown by the test runner.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md) — a plugin-authoring SDK and a set of Reqnroll plugins.

## Why

`@ignore` says that a scenario does not run, not why: a known bug, unfinished automation, a flaky environment. Teams
invent their own tags for that and write a hook to skip on them. This package makes those tags work without code, and
puts the reason where people look for it: next to the skipped test in the test results.

## Installation

```bash
dotnet add package Ohirok.IgnoreReasons.ReqnrollPlugin
```

A generator plugin: it runs at build time and adds nothing to the test output. It needs no code and no MSBuild setup.

## Configuration

```json
{
  "ignoreReasons": {
    "tags": {
      "@ignore:bug": "Known product defect",
      "@ignore:todo": "Scenario not yet implemented",
      "@wip": "Work in progress",
      "@ignore:flaky": ""
    }
  }
}
```

Each key is a tag (the `@` is optional), each value the reason. An empty reason gives the tag's own name
(`ignore:flaky`). Then:

```gherkin
@ignore:bug
Scenario: User can export a report
    ...
```

is skipped, and the runner shows *Known product defect* as the reason:

| Runner | Generated attribute | Shown as |
| --- | --- | --- |
| NUnit | `[Ignore("Known product defect")]` | Ignored, with the message |
| MSTest | `[Ignore("Known product defect")]` | Skipped, with the message |
| xUnit | `[Fact(Skip = "Known product defect")]` (or `[Theory]`) | Skipped, with the reason |
| TUnit | `[Skip("Known product defect")]` | Skipped, with the reason |

## How it works

Reqnroll's own `@ignore` does the ignoring, so it behaves exactly like `@ignore` on every runner: before generation, the
scenarios an alias covers get `@ignore` added; the ignore attribute Reqnroll then generates for each test gets the alias's
reason instead of its generic text. The alias tag stays on the scenario, so reports (the Ohirok report lists the
scenario as *Not run* with its tags, Reqnroll's HTML report too) and tag filters show why it was skipped. As with
`@ignore`, a feature whose tests are all skipped never starts, so Reqnroll writes nothing about it for reports; the test
results still list each test with its reason.

- An alias works on a scenario, a Scenario Outline (all its examples), a rule (all its scenarios) and a feature (all its
  scenarios). Each test gets its own attribute, so a feature-level alias skips its tests one by one, each with the reason.
- The most specific alias gives the reason: the scenario's, then its rule's, then its feature's.
- An alias next to `@ignore` still gives its reason. `@ignore` alone keeps Reqnroll's text.
- A tag that is not configured is an ordinary tag: `@ignore:other` skips nothing.
- On an Examples block, use `@ignore`: aliases apply to whole scenarios.

| Key | Default | Meaning |
| --- | --- | --- |
| `ignoreReasons.tags` | `{}` | Alias tag → reason. Nothing changes until an alias is configured. |
| `ignoreReasons.generationInterceptorOrder` | `600` | Position among the plugins that rewrite the feature before generation (lower first); see the [SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md). After AutoTags (`500`), so a tag AutoTags adds can be an alias. |

## See also

- [StepHashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md): `#ignore` for a single step, with a reason in the comment.
- [AutoTags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/AutoTags/README.md): tags derived from steps.
