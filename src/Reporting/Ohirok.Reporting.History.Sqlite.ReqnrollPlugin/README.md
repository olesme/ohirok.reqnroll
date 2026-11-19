# Ohirok.Reporting.History.Sqlite.ReqnrollPlugin

SQLite-backed `IReportHistoryStorage` for `Ohirok.Reporting.History.Core`. Drop this package in alongside `Ohirok.Reporting.ReqnrollPlugin` to get flaky/regressed/broken health badges without standing up any external database.

## Installation

```bash
dotnet add package Ohirok.Reporting.History.Sqlite.ReqnrollPlugin
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
    "dbPath": "reporting-history.db"
  }
}
```

All SQLite history options are optional. Each setting falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the SQLite history backend on. |
| `autoCreateDb` | `true` | Creates the SQLite database file automatically when needed. |
| `historyWindow` | `4` | Analysis window for health comparison. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of prior runs to display in the report UI. |
| `dbPath` | `"reporting-history.db"` | File path for the SQLite database file. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into, so history dots link to archived runs. |
| `pipelineRunId` | `null` | Logical run id; runs saved with the same id are merged into one history row. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

`dbPath` is relative to the test run's working directory unless an absolute path is given. See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).
