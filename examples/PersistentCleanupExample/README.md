# PersistentCleanupExample

Two standalone sub-projects demonstrating `Ohirok.PersistentCleanup`'s two storage backends. They're
separate, self-contained projects because the
two backends exercise genuinely different code paths, not just a config variant of the same thing.

- **`PersistentCleanupSqliteExample/`** - zero-code, config-only (`reqnroll.json`'s `persistentCleanup`
  section). Demonstrates order-based cleanup execution (registration order doesn't matter - `Order`
  does) and a cleanup that's retried to exhaustion and marked `Failed`.
- **`PersistentCleanupCustomStorageExample/`** - `Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin`
  ships no storage backend at all; the consumer supplies one via `ICleanupStorage`. Since
  `ICleanupService` lives in Reqnroll's global container (shared by every scenario in the run, not
  scenario-scoped), the storage is wired up once via `UseStorage(storage)` in a `[BeforeTestRun]` hook
  - not per-scenario, which would throw "already configured" on the second scenario. Demonstrates both
  `UseStorage(storage)` and the `PerformAllAsync(storage)` convenience overload's "ignore if a storage
  is already configured" semantics.

Run either with `dotnet test` from its own project folder.
