# Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin

SQLite storage adapter for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md). The core README explains how cleanup works, when it runs and how to use scopes in parallel pipelines.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin
```

## Configuration

In `reqnroll.json`:

```json
{
  "persistentCleanup": {
    "dbPath": "cleanup.db"
  }
}
```

| Setting | Default | Meaning |
| --- | ---: | --- |
| `dbPath` | `"cleanup.db"` | Path of the SQLite file. A relative path is resolved against the **current working directory of the test process**, which is usually, but not always, the output directory (it depends on the runner and IDE). Use an absolute path when it matters. |

The file and its directory are created automatically (`autoCreateDb: true`). All other settings (`autoPerformOnStart`, `scope`, `maxConcurrency`, retention, ...) are shared by all adapters and described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Notes

- Several test processes can share one file; record claims are atomic. Under heavy parallel load SQLite can report `database is locked`. If you see it, put the file on a local disk (not a network share) or use a client-server adapter.
- On ephemeral CI agents, keep the file between runs (pipeline cache/artifact) or leftovers will not be found.
