# Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin

Azure Cosmos DB storage adapter for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md). The core README explains how cleanup works, when it runs and how to use scopes in parallel pipelines.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "persistentCleanup": {
    "connectionString": "AccountEndpoint=https://...;AccountKey=...",
    "databaseId": "CleanupDb",
    "containerId": "CleanupRecords"
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `connectionString` | `""` | Cosmos DB connection string. |
| `databaseId` | `"CleanupDb"` | Database name. Created if missing. |
| `containerId` | `"CleanupRecords"` | Container name. Created if missing, partitioned by `/id`. |

All other settings (`autoPerformOnStart`, `scope`, `maxConcurrency`, retention, ...) are shared by all adapters and described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Notes

- The container is partitioned by `/id`, so a status change is an in-place replace, and a record is claimed with a replace guarded by the item ETag (exactly one of several competing processes wins).
- Listing queries are cross-partition. That is fine for a table that only holds leftover cleanups.
