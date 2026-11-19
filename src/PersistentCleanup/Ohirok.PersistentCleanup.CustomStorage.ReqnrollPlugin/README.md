# Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin

Use your own backing store for [Ohirok.PersistentCleanup](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md) without writing a whole plugin.

## What this package does, and what it does not

- It registers the cleanup service and **nothing else**. There is no store until you supply one.
- Because there is no store at startup, **`autoPerformOnStart` has no effect** with this package. You run leftovers yourself (below).
- Records registered before the store is set are kept in memory and flushed into it as soon as it is set.
- The store is initialized lazily: `InitializeAsync` is called once, on first use.

## Installation

```bash
dotnet add package Ohirok.PersistentCleanup.Core
dotnet add package Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin
```

## 1. Implement `ICleanupStorage`

All members are required. Implementations must be thread-safe and safe to use from several processes.

```csharp
using Ohirok.PersistentCleanup;

public class MyCleanupStorage : ICleanupStorage
{
    public Task InitializeAsync(CancellationToken ct = default) { /* create schema */ }

    public Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
    { /* insert; record.Id is already assigned (GUID string) */ }

    public Task<List<CleanupRecord>> GetByStatusesAsync(
        IReadOnlyCollection<CleanupStatus> statuses, string scope = null, CancellationToken ct = default)
    { /* status IN statuses AND (scope == null OR Scope == scope), ordered by Order, CreatedAt */ }

    public Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
    { /* record or null */ }

    public Task<bool> TryClaimAsync(
        string id, CleanupStatus expectedStatus, DateTime? lastAttemptBefore, CancellationToken ct = default)
    { /* atomic compare-and-set, see below */ }

    public Task UpdateStatusAsync(
        string id, CleanupStatus status, string errorMessage = null, int? attemptCount = null, CancellationToken ct = default)
    { /* set Status, LastAttemptAt = now (UTC), ErrorMessage; AttemptCount only if given */ }

    public Task DeleteAsync(string id, CancellationToken ct = default) { /* delete by id */ }

    public Task<int> DeleteWhereAsync(
        DateTime? olderThan, CleanupStatus? status, string scope, CancellationToken ct = default)
    { /* delete where CreatedAt < olderThan AND Status == status AND Scope == scope; null = no condition */ }
}
```

The two methods that need care:

- **`TryClaimAsync`** must be a single atomic compare-and-set: set `Status = Processing` and `LastAttemptAt = now` only if the record still has `expectedStatus` and, when `lastAttemptBefore` is given, `LastAttemptAt` is empty or older than that. Return `true` only if this call changed the record. In SQL this is one `UPDATE ... WHERE` and checking the affected row count; in document stores use their conditional/ETag update. This is what prevents two processes from executing the same cleanup.
- **`scope`**: `null` means every scope. The empty string is a real value (records saved without a scope), so store it as `""`, not as a missing field.

Store timestamps in UTC.

## 2. Attach it and run leftovers

```csharp
using Ohirok.PersistentCleanup;
using Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin;
using Reqnroll;

[Binding]
public static class CleanupStorageSetup
{
    [BeforeTestRun]
    public static async Task Setup(ICleanupService cleanup)
    {
        // Sets the storage and performs all leftover cleanups of this process's scope.
        await cleanup.PerformAllAsync(new MyCleanupStorage(/* ... */));
    }
}
```

Reqnroll injects `ICleanupService` into a static `[BeforeTestRun]` method as a parameter. To set the storage without running leftovers, use `cleanup.UseStorage(new MyCleanupStorage(...))` instead.

`UseStorage` throws if a storage is already set; pass `ignoreIfExists: true` to keep the existing one silently.

Everything else (how cleanups are registered, when they run, scopes for parallel runs, configuration) is described in the [core README](https://github.com/olesme/ohirok.reqnroll/blob/main/src/PersistentCleanup/README.md).
