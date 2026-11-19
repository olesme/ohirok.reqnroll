namespace Ohirok.Reporting.History
{
    /// <summary>Implements the upsert-by-RunId merge semantics shared by every IReportHistoryStorage backend
    /// (Option A). A base run and its rerun-of-failures pass share one RunId (via
    /// ReportHistoryConfigBase.PipelineRunId) but each SaveRunAsync call only carries the scenarios THAT
    /// process actually executed - a blind full-row replace on match would silently drop the base run's
    /// passed scenarios, so the merge is scenario-level (by ScenarioKey), not row-level.</summary>
    public static class ReportHistorySnapshotMerger
    {
        /// <summary>Merges <paramref name="incoming"/> into <paramref name="existing"/> (same RunId). RunAt is
        /// kept from <paramref name="existing"/> (the logical run's original start time, not the rerun
        /// process's later timestamp). RunUrl/RunId prefer <paramref name="incoming"/> when non-null
        /// (last-writer-wins for the archive pointer). Scenarios are merged last-wins by ScenarioKey - a
        /// scenario absent from <paramref name="incoming"/> is left untouched; one present in both is replaced
        /// by incoming's copy.</summary>
        public static ReportHistoryRunSnapshot Merge(ReportHistoryRunSnapshot existing, ReportHistoryRunSnapshot incoming)
        {
            var scenariosByKey = existing.Scenarios.ToDictionary(s => s.ScenarioKey);
            foreach (var scenario in incoming.Scenarios)
                scenariosByKey[scenario.ScenarioKey] = scenario;

            return new ReportHistoryRunSnapshot
            {
                RunAt = existing.RunAt,
                RunId = existing.RunId ?? incoming.RunId,
                RunUrl = incoming.RunUrl ?? existing.RunUrl,
                Scenarios = [.. scenariosByKey.Values]
            };
        }
    }
}
