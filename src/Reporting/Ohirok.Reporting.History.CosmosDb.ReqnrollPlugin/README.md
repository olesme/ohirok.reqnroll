# Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin

Azure Cosmos DB-backed `IReportHistoryStorage` for `Ohirok.Reporting.History.Core`. Drop this package in alongside `Ohirok.Reporting.ReqnrollPlugin` to get flaky/regressed/broken health badges backed by your existing Cosmos DB account.

## Installation

```bash
dotnet add package Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin
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
    "connectionString": "AccountEndpoint=https://...;AccountKey=...",
    "databaseId": "ReportHistoryDb",
    "containerId": "ReportHistoryRuns"
  }
}
```

All Cosmos DB history settings are optional. Each setting falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Turns the Cosmos DB history backend on. |
| `autoCreateDb` | `true` | Creates the database and container automatically when needed. |
| `historyWindow` | `4` | Analysis window for health comparison. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of prior runs shown in the report UI. |
| `connectionString` | `""` | Azure Cosmos DB connection string. |
| `databaseId` | `"ReportHistoryDb"` | Cosmos database name used for history data. |
| `containerId` | `"ReportHistoryRuns"` | Cosmos container name used to store run snapshots. |
| `runArchiveDirectory` | `null` | Directory each run's HTML report is copied into, so history dots link to archived runs. |
| `pipelineRunId` | `null` | Logical run id; runs saved with the same id are merged into one history row. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

The database and container are created automatically on first use (`autoCreateDb: true` by default), so teams already running Cosmos DB can enable history tracking without provisioning anything new by hand. See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).

> Breaking change / repartitioned container: earlier versions stored one document per scenario-per-run, partitioned by `/ScenarioKey`. Since a run-snapshot document now holds every scenario in the run, that key no longer means anything, and Cosmos partition keys are immutable once a container is created — so this container is now partitioned by `/id` (each run document is its own partition) under a new default name (`ReportHistoryRuns`, not `ReportHistoryRecords`). If you were relying on the old container, either point `containerId` at a fresh name (the default) or delete and recreate it; there is no in-place migration path. A consequence of the `/id` partitioning: `GetRecentRunsAsync` is now an inherently cross-partition query (no partition-key filter to scope it), which costs more RU than the old single-partition-per-scenario lookup.
