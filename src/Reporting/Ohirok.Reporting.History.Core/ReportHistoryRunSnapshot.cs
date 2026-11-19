namespace Ohirok.Reporting.History
{
    public class ReportHistoryRunSnapshot
    {
        public DateTimeOffset RunAt { get; set; }

        /// <summary>Opaque per-run identity - lets report.js label/click-through a history
        /// dot back to "this exact run", distinct from RunAt which only has day/second granularity and isn't
        /// guaranteed unique across parallel or re-run pipelines. Null for snapshots saved before this field
        /// existed - older rows simply read back with no RunId.</summary>
        public string? RunId { get; set; }

        /// <summary>Relative or absolute link to this run's own archived full HTML report, set only when
        /// ReportHistoryConfigBase.RunArchiveDirectory is configured - null means no archive exists (feature
        /// off, or the snapshot predates it) and report.js renders a plain, non-clickable history dot.</summary>
        public string? RunUrl { get; set; }

        public List<ReportHistoryScenarioOutcome> Scenarios { get; set; } = [];
    }
}
