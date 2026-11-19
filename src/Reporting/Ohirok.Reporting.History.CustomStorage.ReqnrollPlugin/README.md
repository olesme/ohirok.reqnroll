# Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin

Bring your own backend for `Ohirok.Reporting.History.Core`. Use this when none of the provided storage adapters fit (e.g. an existing internal datastore).

## Installation

```bash
dotnet add package Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin
```

## Usage

Implement `IReportHistoryStorage`, then supply it before the run needs it (e.g. a `[BeforeTestRun]` hook):

```csharp
[Binding]
public static class HistoryStorageSetup
{
    [BeforeTestRun]
    public static void ConfigureHistoryStorage(IReportHistoryStorage storage)
    {
        storage.UseStorage(new MyCustomHistoryStorage(), ignoreIfExists: true);
    }
}
```

`storage` here is the `IReportHistoryStorage` already registered by this plugin (a placeholder with no backing store until `UseStorage` is called). `UseStorage` throws if a storage implementation is already configured, unless `ignoreIfExists: true`.

> Breaking change: `IReportHistoryStorage` moved from a per-scenario `ReportHistoryRecord`-based contract (`SaveRunAsync(ReportHistoryRecord)` / `GetRecentRunsAsync(scenarioKey, count)`) to a per-run snapshot contract (`SaveRunAsync(ReportHistoryRunSnapshot)` / `GetRecentRunsAsync(count)`, where the snapshot embeds every scenario's outcome for that run). Any custom `IReportHistoryStorage` implementation must be updated to the new contract — see `Ohirok.Reporting.History.Core`'s README for the full shape.

## Configuration

In `reqnroll.json`:

```json
{
  "reportingHistory": {
    "enabled": true,
    "historyWindow": 4,
    "historyDisplayWindow": 10
  }
}
```

All custom-storage history settings are optional. Each value falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Enables history capture and registration. |
| `historyWindow` | `4` | Analysis window used to derive scenario health. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of prior runs displayed in history UI. |
| `runArchiveDirectory` | `null` | Optional directory for archived HTML runs. |
| `pipelineRunId` | `null` | Optional shared run-id across rerun-of-failures passes. |

Use `UseStorage` to replace the placeholder backend before the run needs it. If a storage implementation is already configured, `UseStorage` throws unless `ignoreIfExists: true` is passed.

See `Ohirok.Reporting.History.Core`'s README for `historyDisplayWindow` (optional, defaults to `historyWindow`).
