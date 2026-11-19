# Ohirok.Reporting.History.MongoDb.ReqnrollPlugin

MongoDB-backed `IReportHistoryStorage` for `Ohirok.Reporting.History.Core`. Drop this package in alongside `Ohirok.Reporting.ReqnrollPlugin` to get flaky/regressed/broken health badges for teams already running MongoDB.

## Installation

```bash
dotnet add package Ohirok.Reporting.History.MongoDb.ReqnrollPlugin
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
    "connectionString": "mongodb://localhost:27017",
    "databaseName": "ReportHistoryDb",
    "collectionName": "ReportHistoryRuns"
  }
}
```

All MongoDB history settings are optional. Each setting falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the MongoDB history backend on. |
| `autoCreateDb` | `true` | Creates the database and collection if needed. |
| `historyWindow` | `4` | Analysis window for health comparison. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of prior runs shown in the report UI. |
| `connectionString` | `""` | MongoDB connection string. |
| `databaseName` | `"ReportHistoryDb"` | Database name for the report history collection. |
| `collectionName` | `"ReportHistoryRuns"` | Collection name used to store run snapshots. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into, so history dots link to archived runs. |
| `pipelineRunId` | `null` | Logical run id; runs saved with the same id are merged into one history row. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).
