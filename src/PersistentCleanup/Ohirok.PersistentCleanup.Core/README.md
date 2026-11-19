# Ohirok.PersistentCleanup.Core

The core of [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md): cleanup that survives failed, crashed and interrupted Reqnroll runs.

This package contains the engine, not a ready-to-use plugin: the `[Cleanup]` attribute, `ICleanupService` (register, perform, delete, statistics), the record model, the configuration base and the `ICleanupStorage` contract. It knows nothing about any particular database. **On its own it does not persist anything**, so you normally do not install it directly, it arrives as a dependency of a storage adapter.

For how cleanup works, when it runs, scopes and parallel execution, read the [main README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md).

The settings every adapter shares (`CleanupConfigBase`), including `processingTimeoutMinutes`, `scope` and the `OHIROK_CLEANUP_SCOPE` environment variable that overrides it, are listed in its [configuration section](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md#configuration).

## Which package do you need?

| You want | Install |
| --- | --- |
| SQLite, PostgreSQL, SQL Server, MySQL, MongoDB, Cosmos DB or Redis | The matching `Ohirok.PersistentCleanup.<Storage>.ReqnrollPlugin` adapter |
| Any other store, without writing a plugin | [`Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin`](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin/README.md): implement `ICleanupStorage` and attach it in a `[BeforeTestRun]` hook |
| An existing adapter, but it does not behave the way you need | Open an issue with what you need. You can also fork it: the adapters are small and deliberately simple |
| A new storage adapter as a proper plugin (this package) | Reference `Ohirok.PersistentCleanup.Core` and follow the section below |

Use Core directly only to build an adapter for a storage that is not covered yet. If that storage is reasonably common, please open an issue too: it is likely worth having in the box.

## Building a storage adapter

An adapter is three small pieces. The existing SQLite adapter is the reference implementation.

**1. Implement `ICleanupStorage`.** It has eight methods. The contract is described in the doc comments of the interface; the rules that matter:

- `TryClaimAsync` must be an atomic compare-and-set (a single conditional `UPDATE`, an ETag-guarded replace, a Redis transaction with a condition). It is what guarantees that two test processes never execute the same record.
- `scope == null` in a query means "every scope". The empty string is a real scope (records saved without one).
- The record id is assigned by the caller (a GUID string); store it as given.
- Store timestamps in UTC.
- The implementation must be thread-safe. `InitializeAsync` is called once, lazily, before first use.

**2. Add a config class** with the storage-specific settings. It extends `CleanupConfigBase` and is read from the `persistentCleanup` section of `reqnroll.json`:

```csharp
internal sealed class MyStoreCleanupConfig : CleanupConfigBase
{
    public string ConnectionString { get; set; } = "";
}
```

**3. Register the plugin.** One class, which hands your storage factory to the bootstrap:

```csharp
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MyStoreCleanupPlugin))]

public sealed class MyStoreCleanupPlugin : IRuntimePlugin
{
    public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
    {
        PersistentCleanupBootstrap.Setup<MyStoreCleanupConfig>(e,
            cfg => new MyStoreCleanupStorage(cfg.ConnectionString, cfg.AutoCreateDb));
    }
}
```

`Setup` reads the config, registers `ICleanupService` in the global container and, when `autoPerformOnStart` is on, performs the leftovers when the test run starts. Scope handling, retries, ordering, abandoned-record recovery and retention are done by the core, so an adapter only stores and queries records.

Package it like the existing adapters (a Reqnroll runtime plugin, with `Ohirok.PersistentCleanup.Core` as a dependency). Pull requests with new adapters are welcome.
