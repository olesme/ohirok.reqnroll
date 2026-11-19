# Ohirok.Reporting.History.Redis.ReqnrollPlugin

Redis-backed `IReportHistoryStorage` for `Ohirok.Reporting.History.Core`. Drop this package in alongside `Ohirok.Reporting.ReqnrollPlugin` to get flaky/regressed/broken health badges.

## Installation

```bash
dotnet add package Ohirok.Reporting.History.Redis.ReqnrollPlugin
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
    "connectionString": "localhost:6379",
    "keyPrefix": "reporthistory:",
    "database": 0
  }
}
```

All Redis history settings are optional. Each setting falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the Redis history backend on. |
| `autoCreateDb` | `true` | Initializes the Redis key space when needed. |
| `historyWindow` | `4` | Analysis window for health comparison. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of prior runs shown in the report UI. |
| `connectionString` | `""` | Redis connection string. |
| `keyPrefix` | `"reporthistory:"` | Namespace prefix used for report-history keys. |
| `database` | `0` | Redis database index to use. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into, so history dots link to archived runs. |
| `pipelineRunId` | `null` | Logical run id; runs saved with the same id are merged into one history row. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

Use this backend for teams already running Redis who want scenario run history shared across test runs without adding a separate database engine. See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).
