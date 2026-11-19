# Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin

MongoDB storage adapter for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md). The core README explains how cleanup works, when it runs and how to use scopes in parallel pipelines.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "persistentCleanup": {
    "connectionString": "mongodb://localhost:27017",
    "databaseName": "CleanupDb",
    "collectionName": "CleanupRecords"
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `connectionString` | `""` | MongoDB connection string. |
| `databaseName` | `"CleanupDb"` | Database name. |
| `collectionName` | `"CleanupRecords"` | Collection name. Indexes are created automatically. |

All other settings (`autoPerformOnStart`, `scope`, `maxConcurrency`, retention, ...) are shared by all adapters and described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Notes

- The document `_id` is the record id (a string). A record is claimed with one conditional `UpdateOne`, so several test processes can safely share one collection.
