# Ohirok.PersistentCleanup

Cleanup for Reqnroll tests that survives failed, crashed and interrupted runs.

Part of [Ohirok Project](https://github.com/olesme/ohirok.reqnroll/blob/main/README.md) — a plugin-authoring SDK and a set of Reqnroll plugins.

## The problem

Integration tests create data: users, orders, files, records in third-party systems. Somebody has to delete it afterwards. The obvious place for that is the end of the test (or an `AfterScenario` hook), and it has two well-known failure modes.

### Scenario 1: orphaned entities

Cleanup placed at the end of a test, or in a teardown hook, does not run when something goes wrong: the test fails midway, the process is killed, the debugger is stopped, the CI agent dies, or the teardown itself throws. The entities the test created stay in the system forever.

Leftovers often break the next run too: `createUser` hits a unique constraint, the test never gets past setup, so its cleanup never runs, and it stays red until someone cleans the database by hand.

### Scenario 2: a failure you cannot reproduce

The opposite case: the test fails, and everything is cleaned up successfully. You have an error report, but the entities that triggered the failure are gone. Re-running with fresh entities passes. That is a flaky test, and there is nothing left to investigate.

If those entities had been kept (and logged), you could do a post-mortem on exactly the data that caused the failure. They would then be removed before the next run, or manually once you are done with them.

This plugin handles both: it removes leftovers automatically at the start of the next run, and it can keep the entities of a failed scenario until then.

## Why not just clean up by name?

A common workaround is to give every test entity a recognizable marker, such as a `test_` prefix, a `[TEST]` suffix or a dedicated tenant, and to delete everything that matches before each run. If your system allows that, you probably do not need this plugin.

It stops working when the marker cannot be relied on:

- IDs or descriptions are generated dynamically (GUIDs, sequence numbers, timestamps, values computed from other data), so there is no stable substring to search for.
- The system under test assigns the ID itself, and you only learn it from the response.
- Deleting "everything that contains `test`" is unsafe on shared environments and could remove other people's data.
- The entity is not searchable by name or description at all, for example a file in storage, a message in a queue or a record in an external API.
- Several test runs execute in parallel, and a blanket "delete all test data" step destroys the data of the runs that are still in progress.

## How it works

Instead of guessing what a previous run left behind, the plugin remembers exactly what was created.

1. When a scenario creates an entity, the cleanup action for it is registered together with whatever is needed to undo it: the actual ID, the actual generated name and so on.
2. The pending cleanup is persisted and does not live only in memory. It outlives the test process.
3. When a scenario passes, its cleanup runs as usual and is removed from the store.
4. When a scenario fails, its entities are kept and their identifiers are logged so you can investigate the failure on the exact data that caused it.
5. If the run is interrupted, only the persisted record remains.
6. At the start of the next run, the plugin executes all leftover cleanups before any scenario starts. You can also run them manually once you are done investigating.

## Installation

Install the core package and the storage adapter you want to use:

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin
```

The package references `Ohirok.Reqnroll.PluginChains.Sdk` and registers itself as a Reqnroll plugin. No extra configuration is needed to enable the basic flow.

## Configuration

All `persistentCleanup` settings are optional. Each setting falls back to the default below when omitted.

The base cleanup config defaults are defined on `CleanupConfigBase`:

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Enables the cleanup plugin. |
| `autoCreateDb` | `true` | Creates the underlying store automatically when supported. |
| `autoPerformOnStart` | `true` | Runs left-over cleanup records from prior runs at the start of a fresh run. |
| `maxConcurrency` | `5` | Maximum concurrency when executing cleanup jobs. |
| `defaultMaxRetries` | `3` | Default retry count for individual cleanup actions. |
| `deleteCompletedAfterDays` | `7` | Retention threshold for completed cleanup records. |
| `deleteOrphansAfterDays` | `30` | Retention threshold for orphaned records. |
| `processingTimeoutMinutes` | `30` | A record that has been in `Processing` for longer than this (counted from its last attempt) is treated as abandoned by a run that died, and is executed again. Values below 1 count as 1. |
| `scope` | `""` | Scope of this test process; see [Scopes and parallel runs](#scopes-and-parallel-runs). Overridden by the `OHIROK_CLEANUP_SCOPE` environment variable. |
| `logLevel` | `Detailed` | Logging verbosity for cleanup execution. |

Each storage adapter adds its own backend-specific settings (for example `dbPath`, `connectionString`, `databaseName`, or `keyPrefix`) in the same `persistentCleanup` section.

### Scopes and parallel runs

By default all test processes that share a store also share its records: a run that starts performs every leftover it finds. When several runs use the same store at the same time (parallel pipelines, several agents, several suites), that means one run can execute cleanups that another run, still in progress, registered a moment ago. Scopes separate them.

- A process with a `scope` writes that scope into every record it registers, and only sees records of the same scope: the leftovers it performs on start, its statistics and its retention deletes.
- A process without a scope (the default, an empty string) writes records without a scope and only sees those.
- `"*"` sees and performs the records of every scope. It is meant for a dedicated cleanup step, for example at the end of a pipeline. Records it registers itself get no scope.

The `OHIROK_CLEANUP_SCOPE` environment variable, when set to a non-empty value, replaces the configured `scope`, so one `reqnroll.json` can serve every pipeline:

```bash
# a pipeline that only touches its own records
OHIROK_CLEANUP_SCOPE=nightly-eu dotnet test

# a dedicated cleanup step that performs the leftovers of every scope
OHIROK_CLEANUP_SCOPE="*" dotnet test
```

Two processes with the same scope can still run at the same time: claiming a record is atomic, so a record is never executed twice, and `processingTimeoutMinutes` decides when a record claimed by a run that died is picked up again.

## Usage

Mark the method that undoes a creation with `[Cleanup]` and persist the operation against the cleanup service:

```csharp
using Ohirok.PersistentCleanup;

[Binding]
public class UserSteps(ICleanupService cleanupService)
{
    [Given("a user exists")]
    public async Task GivenAUserExists()
    {
        var id = Guid.NewGuid().ToString();
        api.CreateUser(id);

        await cleanupService.AddAsync<UserCleanupActions>(
            x => x.DeleteUser(id),
            description: $"Delete test user {id}");
    }
}

public static class UserCleanupActions
{
    [Cleanup("DeleteUser")]
    public static void DeleteUser(string userId)
    {
        api.DeleteUser(userId);
    }
}
```

Rules for cleanup methods:

- They must be idempotent.
- They should depend only on persisted data, not on in-memory scenario state.
- The attribute key is optional; if omitted, the method name is used.

## When it runs

| Moment | What happens |
| --- | --- |
| Start of the test run | Leftover cleanups from previous runs are executed |
| Scenario finishes | Its own cleanup is executed and its record is removed |
| Run is interrupted | Records stay in the store and are handled by the next run |

## Comparison of approaches

| Approach | Guaranteed to run | Data inspectable after failure | Needs a name marker |
| --- | --- | --- | --- |
| Cleanup at the end of the test | no | no | no |
| `AfterScenario` hook | mostly | no | no |
| Clean up first, by name or ID | yes | yes | yes |
| PersistentCleanup | yes | yes | no |

## Limitations

- The plugin cleans up only what it was told about. Entities created outside registered steps are not tracked.
- The store must be reachable between runs. On ephemeral CI agents, point it at a persistent location or a shared one.
- Storage location and retention policy are configured by the selected adapter.

## See also

- [`src/PluginChains.Sdk/README.md`](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PluginChains.Sdk/README.md) for the shared plugin runtime and generator infrastructure.
- [`src/Reporting/Ohirok.Reporting.History.Core/README.md`](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.History.Core/README.md) for the same pattern of core-plus-storage history packages.
- Implementation folders under `src/PersistentCleanup/` for the specific storage adapters and bootstrap code.
