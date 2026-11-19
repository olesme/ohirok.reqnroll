# Ohirok.Reporting.History.Core

Run-history storage contract and health analysis for `Ohirok.Reporting`.

This package defines the storage contract and the health-analysis model used by the reporting package set. Storage-specific packages plug into this contract.

## Installation

Choose a backend and install it with the formatter package:

```bash
dotnet add package Ohirok.Reporting.History.Sqlite.ReqnrollPlugin
dotnet add package Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin
```

## Related docs

- [Ohirok.Reporting](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/README.md)
- [Ohirok.Reporting.ReqnrollPlugin](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.ReqnrollPlugin/README.md)
- [Ohirok.Reporting.History.Sqlite.ReqnrollPlugin](https://github.com/olesme/ohirok.reqnroll/blob/main/src/Reporting/Ohirok.Reporting.History.Sqlite.ReqnrollPlugin/README.md)

## IReportHistoryStorage

Implement this interface and use `Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin` to plug in any backing store.

```csharp
public interface IReportHistoryStorage
{
    Task InitializeAsync(CancellationToken ct = default);
    Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default);
    Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default);
}
```

This is an artifact model: one snapshot is persisted per whole test run, not per scenario. `ReportHistoryRunSnapshot` carries `RunAt`, `RunId`, `RunUrl` (the configured `runUrl`, or the archived HTML report when `runArchiveDirectory` is set) and the `Scenarios` list for that run; each `ReportHistoryScenarioOutcome` carries `ScenarioKey`, `ScenarioName`, an `Outcome` (`Passed`, `Failed`, `Incomplete`, `PassWithIssues`, `Skipped`, `Ambiguous`) and an optional `SnapshotJson` with the scenario's details. `OhirokReportFormatter` calls `SaveRunAsync` exactly once per run (with every scenario's outcome embedded) and `GetRecentRunsAsync` exactly once per run, rather than one round-trip per scenario.

`SaveRunAsync` is an upsert: when a row with the snapshot's `RunId` already exists, the implementation merges the new scenarios into it (`ReportHistorySnapshotMerger.Merge`) instead of inserting a second row. This is how a base run and its rerun of failures, sharing one `pipelineRunId`, become one history entry. A custom implementation must follow the same rule.

> Breaking change: this replaced the earlier per-scenario `ReportHistoryRecord`-based contract (`SaveRunAsync(ReportHistoryRecord)` / `GetRecentRunsAsync(scenarioKey, count)`). Any hand-rolled `IReportHistoryStorage` implementation (via `Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin`) must be updated to the snapshot-based contract above.

## ReportHealthAnalyzer

The report page derives each scenario's health from the recent runs embedded in it. `ReportHealthAnalyzer.Analyze(runsNewestFirst)` is a C# implementation of the health rules for code that works with stored snapshots directly: it derives a `ScenarioHealth` (`New`, `Flaky`, `Regressed`, `Broken`, `Fixed`, `Passing`, `Invalid`, `Unknown`) by comparing only the real (non-`Incomplete`) runs in the window. An `Incomplete` run never resets or counts toward the comparison, and a latest run that is `Incomplete` yields `Invalid` rather than being silently skipped.

## Configuration

In `reqnroll.json`:

```json
{
  "reportingHistory": {
    "enabled": true,
    "autoCreateDb": true,
    "historyWindow": 4,
    "historyDisplayWindow": 10
  }
}
```

All history settings are optional. Each one falls back to the default below when omitted.

| Setting | Default | Meaning |
| --- | ---: | --- |
| `enabled` | `true` | Enables reporting-history capture and health analysis. |
| `autoCreateDb` | `true` | Automatically creates the backing store when the storage adapter supports it. |
| `historyWindow` | `4` | Analysis window used to derive `ScenarioHealth`. |
| `historyDisplayWindow` | `null` (falls back to `historyWindow`) | Number of runs displayed in the report's history strip and analytics chart. |
| `runArchiveDirectory` | `null` | Optional directory for archived run HTML pages, linked from history by a relative path. |
| `runUrl` | `null` | Optional URL (with `${ENV_VAR}` references) where the pipeline publishes the run's report; takes precedence over `runArchiveDirectory`. With neither, history has no links to full reports. |
| `pipelineRunId` | `null` | Optional shared logical run id that merges a base run with a rerun-of-failures pass. `${ENV_VAR}` references are resolved, e.g. `"${GITHUB_RUN_ID}"`. |

`historyWindow` is the analysis window compared to derive `Health`. `historyDisplayWindow` is a separate, independent window: how many prior runs are shown in report.js's per-scenario history-dot strip and the Analytics tab's History matrix/trend chart. It's optional and falls back to `historyWindow` when omitted, preserving the old one-number-for-both behavior. Unlike the analysis window, the display window includes `Incomplete` runs rather than dropping them — they render as a third, distinct color/dot instead of silently vanishing.

Storage-specific settings (connection string, database path, etc.) are in the same section. See the storage adapter packages for their specific fields.
