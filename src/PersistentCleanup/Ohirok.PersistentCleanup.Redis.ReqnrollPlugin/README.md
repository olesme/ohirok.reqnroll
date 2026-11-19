# Ohirok.PersistentCleanup.Redis.ReqnrollPlugin

Redis storage adapter for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md). The core README explains how cleanup works, when it runs and how to use scopes in parallel pipelines.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.Redis.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "persistentCleanup": {
    "connectionString": "localhost:6379",
    "keyPrefix": "cleanup:",
    "database": 0
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `connectionString` | `"localhost:6379"` | Redis connection string. |
| `keyPrefix` | `"cleanup:"` | Prefix of all keys written by the plugin. |
| `database` | `0` | Redis database number. |

All other settings (`autoPerformOnStart`, `scope`, `maxConcurrency`, retention, ...) are shared by all adapters and described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Notes

- Redis is an in-memory store: records do not survive a Redis restart unless persistence (RDB/AOF) is configured on the Redis side.
- Every record is one JSON string key plus membership in a per-status set. A record is claimed with a Redis transaction that only applies if the stored value is unchanged (compare-and-set).
