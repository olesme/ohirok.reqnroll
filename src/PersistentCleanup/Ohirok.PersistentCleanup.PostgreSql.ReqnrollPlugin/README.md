# Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin

PostgreSQL storage adapter for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md). The core README explains how cleanup works, when it runs and how to use scopes in parallel pipelines.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "persistentCleanup": {
    "connectionString": "Host=localhost;Database=cleanup;Username=test;Password=test"
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `connectionString` | `""` | Npgsql connection string. The database must exist; the table `cleanup_records` is created automatically (`autoCreateDb: true`). |

All other settings (`autoPerformOnStart`, `scope`, `maxConcurrency`, retention, ...) are shared by all adapters and described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Notes

- A record is claimed with a single conditional `UPDATE`, so several test processes can safely share one table.
- Timestamps are `TIMESTAMPTZ` (UTC). Npgsql 6+ refuses to write a UTC `DateTime` into a plain `TIMESTAMP` column.
