# Ohirok.Reporting.History.FileSystem.ReqnrollPlugin

Ndjson-file-backed `IReportHistoryStorage` for `Ohirok.Reporting.History.Core`. Drop this package in
alongside `Ohirok.Reporting.ReqnrollPlugin` to get flaky/regressed/broken health badges without standing
up any external database.

Unlike the DB-backed history plugins, the history lives in one plain, human-readable file - one JSON
object per line (ndjson). That makes it a portable pipeline artifact: commit it as a CI build artifact,
copy it between build agents or containers, or sync it with any ordinary file-copy mechanism - no
database server required.

## Installation

```bash
dotnet add package Ohirok.Reporting.History.FileSystem.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "reportingHistory": {
    "enabled": true,
    "autoCreateDb": true,
    "historyWindow": 4,
    "historyDisplayWindow": 10,
    "filePath": "reporting-history.ndjson"
  }
}
```

All file-system history options are optional. Each setting falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the file-based history backend on. |
| `autoCreateDb` | `true` | Creates the history file automatically when it does not exist. |
| `historyWindow` | `4` | Analysis window for health comparison. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of previous runs shown in the report UI. |
| `filePath` | `"reporting-history.ndjson"` | Path to the ndjson history file. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into, so history dots link to archived runs. |
| `pipelineRunId` | `null` | Logical run id; runs saved with the same id are merged into one history row. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

`filePath` is relative to the test run's working directory unless an absolute path is given. See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).

## Concurrency

Writes from this process are serialized, but concurrent writers from *separate* processes to the same
file are not coordinated - no different in kind from using the Sqlite plugin's database file without
WAL under concurrent writers. Give each parallel test run its own `filePath` if you run suites
concurrently against shared history.

## CI pipeline usage

The ndjson file is a plain pipeline artifact (same convention Cucumber's own tooling uses for
history): restore it before the test step runs (so this run's `GetRecentRunsAsync` sees prior runs),
then re-publish it after the test step appends this run's line - otherwise every CI run starts from an
empty file and health/trend history never accumulates across builds.

### GitHub Actions

```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      # Restore the history file written by a previous run, before tests execute.
      - uses: actions/download-artifact@v4
        with:
          name: report-history
          path: . # must match reportingHistory.filePath's directory
        continue-on-error: true # first-ever run has no prior artifact yet

      - run: dotnet test

      # Publish the updated history file (with this run's line appended) for the next run to restore.
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: report-history
          path: reporting-history.ndjson
```

### Azure Pipelines

```yaml
steps:
  - checkout: self

  # Restore the history file written by a previous run, before tests execute.
  - task: DownloadPipelineArtifact@2
    inputs:
      artifact: report-history
      path: $(Build.SourcesDirectory)
    continueOnError: true # first-ever run has no prior artifact yet

  - script: dotnet test
    displayName: Run tests

  # Publish the updated history file (with this run's line appended) for the next run to restore.
  - publish: $(Build.SourcesDirectory)/reporting-history.ndjson
    artifact: report-history
    condition: always()
```

Either snippet applies equally to the DB-backed history plugins' own artifacts (e.g. a Sqlite `.db`
file) if you'd rather restore/publish that instead of switching to this package.

## Keeping the self-contained report bounded

This plugin's own history file can grow without limit (each run appends one line, or merges into the
existing line of the same `pipelineRunId`; nothing is ever trimmed on write), but the report HTML only ever embeds `historyWindow`/`historyDisplayWindow` runs per scenario,
so the generated report itself stays a bounded size regardless of how long the ndjson file gets. Keep
those two settings small (roughly 10-20 runs) rather than growing them to "see more history" - if you
need deeper cross-run navigation than that, switch the scenario's history backend to one of the DB-
backed plugins (CosmosDb/MongoDb/MsSql/MySql/PostgreSql/Redis/Sqlite/CustomStorage) instead; they share
the same `IReportHistoryStorage` interface, so no other code changes.

The more likely source of a bloated report is **attachments** (screenshots, traces), not history
status text - a few thousand scenarios' worth of history entries is only a few MB, but embedded
screenshots have been reported to push other tools' reports into the hundreds of MB
(cucumber/html-formatter#62). `ReportAttachment` already exposes a `Url` field alongside `Body` -
once a report's attachments get heavy, point large ones at external blob storage via `Url` instead of
embedding the body inline, rather than relying on every attachment being self-contained in the HTML
file. There is currently no automatic size-threshold/omission logic in this project; this is a
documentation-only recommendation.
