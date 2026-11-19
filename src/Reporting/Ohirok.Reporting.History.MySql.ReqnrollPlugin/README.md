# Ohirok.Reporting.History.MySql.ReqnrollPlugin

MySQL-backed `IReportHistoryStorage` for `Ohirok.Reporting.History.Core`. Drop this package in alongside `Ohirok.Reporting.ReqnrollPlugin` to get flaky/regressed/broken health badges backed by your existing MySQL database.

## Installation

```bash
dotnet add package Ohirok.Reporting.History.MySql.ReqnrollPlugin
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
    "connectionString": "Server=localhost;Database=reporting;User=test;Password=test"
  }
}
```

All MySQL history settings are optional. Each setting falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the MySQL history backend on. |
| `autoCreateDb` | `true` | Creates the report history table automatically when needed. |
| `historyWindow` | `4` | Analysis window for health comparison. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of prior runs shown in the report UI. |
| `connectionString` | `""` | MySQL connection string for the history database. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into, so history dots link to archived runs. |
| `pipelineRunId` | `null` | Logical run id; runs saved with the same id are merged into one history row. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

The history table is created automatically on first use (`autoCreateDb: true` by default), so teams already running MySQL can start tracking scenario history without any extra setup. See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).
