# Ohirok.Reporting

Reporting packages for Reqnroll: HTML report generation, attachments, history, and multi-run combining.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md) — a plugin-authoring SDK and a set of Reqnroll plugins.

## How it works

The reporting stack turns a Reqnroll run into a navigable HTML document you can open after the run or inspect while troubleshooting.

The core flow is:

1. `Ohirok.Reporting.ReqnrollPlugin` registers the `ohirokReport` formatter in Reqnroll.
2. The formatter emits the raw Cucumber Messages stream to `.ndjson`.
3. `Ohirok.Reporting.Core` builds the report model and renders a standalone HTML file.
4. Optional history plugins persist run snapshots; the report page derives health statuses such as `New`, `Flaky`, `Regressed`, `Broken`, `Fixed`, and `Passing` from them.

## Installation

```bash
dotnet add package Ohirok.Reporting.ReqnrollPlugin
```

Then enable the formatter in `reqnroll.json`:

```json
{
  "formatters": {
    "ohirokReport": { "outputFilePath": "report/ohirok-report.ndjson" }
  }
}
```

The formatter writes the `.ndjson` file during the run and produces a sibling `.html` report when the run finishes.

## Report configuration

The look and content of the report are configured in the `reporting` section of `reqnroll.json`. Every key is optional.

```json
{
  "reporting": {
    "title": "Checkout suite - build ${BUILD_NUMBER}",
    "css": "midnight-blue",
    "defaultTheme": "dark",
    "filters": []
  }
}
```

| Key | Default | Meaning |
| --- | --- | --- |
| `title` | `"Ohirok Report"` | Report title, shown in the browser tab, the page header and as the run's root node. `${ENV_VAR}` references are replaced with the value of that environment variable; an undefined variable becomes an empty string. An empty result falls back to the default. |
| `css` | none | Extra CSS appended after the built-in stylesheet, so it overrides the theme variables (or any selector) through the cascade. The value is either the name of a built-in theme or a file path (see below). |
| `template` | built-in template | Full replacement of the HTML shell (`report-template.html`). The value is a file path; no built-in template names exist yet. |
| `js` | built-in script | Full replacement of `report.js`. The value is a file path; no built-in script names exist yet. |
| `defaultTheme` | browser preference | `light` or `dark` (case-insensitive): the theme the report opens with before the viewer has toggled it. A theme the viewer picked earlier still wins. Any other value means the browser's `prefers-color-scheme` decides. |
| `timeFormat` | `24h` | `24h` or `12h`. Applies to every date and time in the report (generation time, run start and finish, history entries), so they never mix clock styles. |
| `locale` | `en-GB` | BCP 47 locale for month and day names and date order, e.g. `en-US` or `uk-UA`. The clock style still follows `timeFormat`. |
| `expandAttachments` | `["info", "warning", "error"]` | Attachment kinds shown open, with no disclosure around them: `info`, `warning`, `error` (callouts), `image`, `html`, `log` (`WriteLine` text), `text` (text files such as `.log` or `.json`), `media` (video and audio), `file`, or `all`. `[]` folds everything. An embed's own `expanded: true/false` wins. |
| `filters` | `[]` | Tag-based filter groups shown in the report; see [Custom filters](#custom-filters). |

### Themes and overrides

`css`, `template` and `js` share one resolution rule: the value is first compared (case-insensitively) with the names of the built-in assets of that kind, and otherwise treated as a file path. A relative path is resolved against the current working directory of the process that renders the report (the test process for the formatter, the shell for `Ohirok.Reporting.Combine`). A value that matches no built-in name and no existing file is ignored, and the built-in default is used.

Built-in `css` themes, embedded from `src/Reporting/Ohirok.Reporting.Core/Rendering/Assets/Themes/`:

- `cyberpunk-purple`
- `cucumber-green`
- `midnight-blue`
- `chocolate-brown`
- `portal-blue` (made to sit inside a CI portal tab: neutral greys, one blue, dense)

`css` is the sync-safe way to restyle the report: it never copies the base files, so it keeps working as the built-in report changes. `template` and `js` are full forks with no automatic sync; track [the assets changelog](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.Core/Rendering/Assets/CHANGELOG.md) if you maintain one. The [report showcase](https://github.com/olesme/ohirok.reqnroll/blob/main/examples/ReportShowcase/README.md) renders the same runs in every built-in theme and in a theme of its own loaded by path.

### Template tokens

A `template` replacement is a plain HTML file. The generator replaces these tokens in it:

| Token | Replaced with |
| --- | --- |
| `{{ohirok:title}}` | The resolved `title`, HTML-encoded. |
| `{{ohirok:generatedDate}}` | The generation timestamp, HTML-encoded. |
| `{{ohirok:defaultTheme}}` | `light`, `dark`, or an empty string when `defaultTheme` is not set to one of them. Meant for a JavaScript string literal. |
| `{{ohirok:css}}` | The built-in `report.css`. |
| `{{ohirok:themeCss}}` | The content resolved from `css`, or an empty string. |
| `{{ohirok:js}}` | The built-in `report.js`, or the content resolved from `js`. |
| `{{ohirok:json}}` | The report data as JSON. The built-in script reads it from `<script type="application/json" id="ohirok-report-data">`. |

The built-in `report-template.html` is the reference for where each token goes. A template that leaves out `{{ohirok:themeCss}}` ignores `css`; one that leaves out `{{ohirok:defaultTheme}}` ignores `defaultTheme`. `examples/ReportShowcase/Customization/` contains a sample template and a sample theme, both loaded by file path.

## Features

- a tree view for features, rules, outlines, examples, retries, and attempts;
- searchable/filterable report content;
- pass-with-issues states for retries or soft-failed scenarios;
- attachments and embeds for logs, screenshots and custom HTML;
- built-in theme overrides and template overrides;
- per-run history health with pluggable storage backends;
- combined multi-run reports via `Ohirok.Reporting.Combine`.

## Attachments

Everything a step or hook attaches is shown under it:

- `IReqnrollOutputHelper.WriteLine` text, as a log block;
- `AddAttachment(path)` files: images inline (with expand and download), video and audio with a player, small
  text files (`.txt`, `.log`, `.json`, `.xml`, `.csv`, `.yaml`, `.md`, up to 256 KB) as a text preview, `.html`
  files as a sandboxed page preview (no scripts), and any other file as a download card;
- the Ohirok embeds from `Ohirok.PluginChains.Common.Reporting`: `EmbedImage`, `EmbedImageUrl`, `EmbedHtml`
  (links to bugs or backlog items, tables, any markup) and `EmbedInfo` / `EmbedWarning` / `EmbedError` callouts.

The showcase's *Every kind of attachment* feature (`examples/ReportShowcase`) shows each of them.

## Scenario status

A scenario's status is the one the test runner reports. A failing hook fails the scenario even when every step
passed (a broken cleanup is a red test in NUnit, MSTest, xUnit and TUnit too); the report shows which hook failed.
Steps a plugin adds to the generated test - for example SoftAssert's end-of-scenario flush, shown with the `*`
keyword - count like any other step. A scenario that passed only after a retry, with a skipped step, or with a
plugin-reported issue is shown as *pass with issues*. If you need failing hooks to count as *pass with issues*
instead, please open an issue.

## History and health

Historical comparisons use the `reportingHistory` section in `reqnroll.json` and the `Ohirok.Reporting.History.*` packages.

```json
{
  "reportingHistory": {
    "enabled": true,
    "autoCreateDb": true,
    "historyWindow": 4,
    "historyDisplayWindow": 10,
    "dbPath": "reporting-history.db",
    "runArchiveDirectory": "report/history-archive",
    "pipelineRunId": "${GITHUB_RUN_ID}"
  }
}
```

All history settings are optional. The selected backend determines whether it needs a path or connection string, but the defaults below apply when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Enables run-history collection and health analysis. |
| `autoCreateDb` | `true` | Creates the storage database/file automatically when the backend supports it. |
| `historyWindow` | `4` | Number of recent runs used for health analysis. |
| `historyDisplayWindow` | `null` => falls back to `historyWindow` | Number of prior runs shown in the UI history strip/trend. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into as `<RunId>.html`, so a history dot links to that archived run. A relative path is relative to the process working directory. When unset, history dots are not clickable. |
| `runUrl` | `null` | Where the pipeline publishes this run's full report (absolute URL, or a path relative to later reports). `${ENV_VAR}` references are resolved, e.g. `"https://ci.example/runs/${GITHUB_RUN_ID}/ohirok-report.html"`. Recorded with the run so history dots and the *Full report* link point at it; takes precedence over `runArchiveDirectory`. |
| `pipelineRunId` | `null` | Logical id of the run. `${ENV_VAR}` references are resolved like in `title`, so one `reqnroll.json` can say `"${GITHUB_RUN_ID}"` and serve every build. Runs saved with the same id are merged into one history row. When unset or empty, every run gets a random id. |
| `dbPath` / `filePath` / `connectionString` | SQLite: `reporting-history.db`; filesystem: `reporting-history.ndjson`; SQL-based backends: none by default | Storage-specific connection/path configuration required by the selected adapter. |

Backend-specific values such as `dbPath`, `filePath`, or `connectionString` are only required when the selected backend needs them. For example, SQLite can safely use a default local file such as `reporting-history.db`, while SQL Server/PostgreSQL/MySQL/Mongo/Cosmos/Redis require a real connection string or endpoint-specific configuration.

### Links from history to full reports

The history stores scenario outcomes, not reports. A history dot or *Full report* link points at an earlier run's
full report only when that report really exists somewhere the reader can open it:

| Setup | What the history links to |
| --- | --- |
| Neither `runUrl` nor `runArchiveDirectory` | Nothing: dots and history rows show the outcome only, with no link. |
| `runArchiveDirectory` | The copy of each run's report in that directory, by a path relative to the report. Works when the archive travels with the history and the reports (a local machine, or a CI cache/artifact that keeps the whole `report/` folder). |
| `runUrl` (recommended in CI) | The address the pipeline publishes each run's report at, built from CI variables, e.g. `"https://<storage>/reports/${BUILD_BUILDID}/ohirok-report.html"` or the run's artifact page. |

Only runs saved with a link get one; older runs saved without it stay link-less rather than pointing at a report
that was never kept.

### Reruns of failed scenarios

A common CI pattern runs the suite, then reruns only the failed scenarios in a second test process. Without `pipelineRunId` the history gets two rows for that build: a full one and a partial one. With `pipelineRunId` set to a value both processes share (for example `"${GITHUB_RUN_ID}"` or `"${BUILD_BUILDID}"`), the second save finds the row of the first and merges its scenarios into it, so the build stays one entry in the history.

This is independent of `Ohirok.Reporting.Combine`: `pipelineRunId` keeps the history store clean, while Combine renders several `.ndjson` files (including a base run and its reruns) as one HTML report.

To see a build and its reruns in **one report**, pass the base run's `.ndjson` and each rerun's `.ndjson` to
`Ohirok.Reporting.Combine` under the same `--name` (oldest first). The rerun attempts are merged into their scenarios
and labelled *rerun 1*, *rerun 2*, ...; a reruns switch in the report header shows or hides them, so the same report
gives both the pipeline's combined outcome and its first pass. Results of different test projects or builds are
combined differently - each with its own `--name`, as separate top-level nodes. Both are described in the
[Combine README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.Combine/README.md).

The history model is intentionally pluggable: a storage adapter can be SQLite, file system, SQL Server, PostgreSQL, MySQL, MongoDB, Redis, Cosmos DB, or a custom implementation.

If more than one history storage plugin is registered in the same application, the runtime will not silently write to multiple backends at once. The configured proxy rejects duplicate storage registration unless the caller explicitly replaces the active one, so the failure is explicit and the reason is clear.

## Custom filters

You can add custom filters based on tags. For example:

```json
{
  "reporting": {
    "title": "Ohirok Reporting Demo",
    "filters": [
      {
        "name": "Severity",
        "options": [
          { "label": "Critical", "match": "@severity:critical", "bg": "#c62828", "fg": "#ffffff" },
          { "label": "Major", "match": "@severity:major", "bg": "darkorange", "fg": "#ffffff" },
          { "label": "Minor", "match": "@severity:minor", "bg": "rgb(96, 125, 139)", "fg": "#ffffff" }
        ],
        "otherLabel": "Unspecified"
      },
      {
        "name": "Priority",
        "options": [
          { "label": "P1", "match": "@p1", "bg": "#6a1b9a", "fg": "#ffffff" },
          { "label": "P2", "match": "@p2", "bg": "#1565c0", "fg": "#ffffff" }
        ]
      }
    ]
  }
}
```

Each scenario lands in exactly one option of every group: the first option whose `match` pattern matches one of its tags or step hashtags, or the group's "other" bucket when none does. A pattern matches a tag written with or without `@` (`@p1` and `p1` both work), a step hashtag written with `#`, is case-insensitive, and supports the `*` and `?` wildcards.

| Key | Default | Meaning |
| --- | --- | --- |
| `name` | `""` | Group name shown in the report. |
| `options[].label` | `""` | Option label. |
| `options[].match` | `""` | Tag or hashtag pattern. |
| `options[].bg` / `options[].fg` | `"#9e9e9e"` / automatic | Background and text color of the option's badge; any CSS color. Without `fg` the text is black or white, whichever is readable on `bg`. |
| `otherLabel` | `"Other"` | Label of the bucket for scenarios no option matches. |
| `otherBg` / `otherFg` | `"#9e9e9e"` / automatic | Colors of that bucket. Without `otherFg` the text is black or white, whichever is readable on `otherBg`. |

## Links to bugs and backlog items

The report has no built-in mapping from tags such as `@bug:1337` or `@pbi:4821` to tracker URLs. Such links are added by a small hook in your own project: it reads the scenario's tags and writes the links with `EmbedHtml`, which the report renders next to the scenario. Because the hook is yours, it can point at any tracker and add whatever you need, for example a "report a bug" link that only appears when the scenario failed.

[`EvidenceSteps.cs`](https://github.com/olesme/ohirok.reqnroll/blob/main/examples/ReportShowcase/Showcase.Steps/EvidenceSteps.cs) in the report showcase is a complete example, and the embed API is described in the [PluginChains SDK README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md#reporting-signals).

## Viewing reports in CI

The report is one self-contained HTML file, so any CI system that can keep a file can show it. When it is shown inside a
frame (a pipeline tab, Jenkins HTML Publisher, ...), the report gets an extra button in its header that reopens it in a
window of its own, outside the CI page's chrome.

- **Azure DevOps:** publish the HTML as a pipeline artifact (`PublishPipelineArtifact`) and open it from the run's
  artifacts. A results tab inside the run, like the one SpecFlow+ LivingDoc had, needs an Azure DevOps extension; there
  is none yet - please open an issue if you need it.
- **GitHub Actions:** upload it with `actions/upload-artifact`, or publish it to GitHub Pages for a link that opens in
  the browser.
- **GitLab:** keep it as an artifact and link it from the merge request with `artifacts:expose_as`, or publish it with
  GitLab Pages.
- **TeamCity:** add the HTML file as a report tab (*Report tabs* in the project settings) - it then shows on every build.
- **Jenkins:** HTML Publisher shows it, but Jenkins' default Content Security Policy blocks the report's script; relax
  `hudson.model.DirectoryBrowserSupport.CSP` for the job or open the archived file directly.

## Examples and demo

[`examples/ReportShowcase/`](https://github.com/olesme/ohirok.reqnroll/blob/main/examples/ReportShowcase/README.md) is the
place to evaluate the report: four test projects on four runners, run over several simulated builds, give every
report you can get - each theme, a single-feature edge case, a 2000-test suite, history across builds, two projects
and a base run with its rerun combined into one. One command (`showcase.ps1`) produces them all.

## Implementation references

The actual implementation is split across the package set:

- `src/Reporting/Ohirok.Reporting.ReqnrollPlugin/OhirokReportFormatterPlugin.cs` — registers the formatter with Reqnroll
- `src/Reporting/Ohirok.Reporting.ReqnrollPlugin/OhirokReportFormatter.cs` — runtime formatter and output lifecycle
- `src/Reporting/Ohirok.Reporting.Core/Config/ReportingSection.cs` — the `reporting` configuration section
- `src/Reporting/Ohirok.Reporting.Core/Rendering/ReportHtmlGenerator.cs` — HTML rendering and template/theme override resolution
- `src/Reporting/Ohirok.Reporting.History.Core/ReportHistoryConfigBase.cs` — the `reportingHistory` configuration section
- `src/Reporting/Ohirok.Reporting.History.Core/IReportHistoryStorage.cs` — history storage contract
- `src/Reporting/Ohirok.Reporting.Combine/Program.cs` — CLI tool for merging runs into one report

## Related documentation

- [PluginChains SDK](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md): the reporting signals (`ReportIssue`, `Embed*`) that this report consumes
- [StepHashtags](https://github.com/olesme/ohirok.reqnroll/blob/main/src/StepHashtags/README.md): step-level metadata shown in the report
- [XFail](https://github.com/olesme/ohirok.reqnroll/blob/main/src/XFail/README.md): expected-failure semantics
- [Retry](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Retry/README.md): retry-aware report states and scenarios
- [SoftAssert](https://github.com/olesme/ohirok.reqnroll/blob/main/src/SoftAssert/README.md): soft-failed scenarios shown as "pass with issues"
- [PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md): durable cleanup model that complements the history model
