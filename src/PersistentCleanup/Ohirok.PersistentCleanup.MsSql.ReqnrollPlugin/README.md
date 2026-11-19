# Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin

SQL Server storage adapter for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md). The core README explains how cleanup works, when it runs and how to use scopes in parallel pipelines.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "persistentCleanup": {
    "connectionString": "Server=localhost;Database=CleanupDb;Integrated Security=true;TrustServerCertificate=true"
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `connectionString` | `""` | SQL Server connection string. The database must exist; the table is created automatically (`autoCreateDb: true`). |

All other settings (`autoPerformOnStart`, `scope`, `maxConcurrency`, retention, ...) are shared by all adapters and described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Notes

- A record is claimed with a single conditional `UPDATE`, so several test processes can safely share one table.
- Timestamps are stored as UTC `DATETIME2`.
