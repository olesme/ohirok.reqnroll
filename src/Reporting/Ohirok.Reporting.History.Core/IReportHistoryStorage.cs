namespace Ohirok.Reporting.History
{
    public interface IReportHistoryStorage
    {
        Task InitializeAsync(CancellationToken ct = default);

        /// <summary>Saves a run snapshot. When <paramref name="snapshot"/>.RunId is non-null and a row with
        /// the same RunId already exists, implementations upsert: merge scenarios into that row (see
        /// <see cref="ReportHistorySnapshotMerger"/>) instead of inserting a second one - this is how a base
        /// run and its rerun-of-failures pass (sharing one RunId via
        /// <c>ReportHistoryConfigBase.PipelineRunId</c>) collapse into a single logical history entry. A null
        /// RunId always inserts a new row, matching every prior release's behavior.</summary>
        Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default);

        /// <summary>Returns up to <paramref name="count"/> most recent runs, newest first.</summary>
        Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default);
    }
}
