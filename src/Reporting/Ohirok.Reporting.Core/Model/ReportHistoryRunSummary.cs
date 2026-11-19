namespace Ohirok.Reporting.Model
{
    /// <summary>One prior run of a scenario, trimmed from Ohirok.Reporting.History's recent-runs lookup at
    /// report-generation time - lets report.js render a LivingDoc-style history matrix (dates x scenarios)
    /// without needing a history backend queried again at view time.</summary>
    public class ReportHistoryRunSummary
    {
        public DateTimeOffset RunAt { get; set; }
        public string Outcome { get; set; } = "";

        /// <summary>Opaque per-run identity, threaded through from ReportHistoryRunSnapshot.RunId
        /// - null for entries saved before this field existed. Lets report.js label/group history dots by exact
        /// run, and pairs with RunUrl below for click-through navigation.</summary>
        public string? RunId { get; set; }

        /// <summary>Threaded through from ReportHistoryRunSnapshot.RunUrl - null when that run has no archived
        /// report (RunArchiveDirectory not configured at the time, or an older entry). report.js renders the
        /// history dot as a link only when this is non-null.</summary>
        public string? RunUrl { get; set; }

        /// <summary>Raw snapshot JSON object for the selected historical scenario view; null when the history
        /// entry is summary-only. The value is intentionally stored as a JSON object string so this contract
        /// can stay in the reporting core assembly without introducing a Core-&gt;History cycle.</summary>
        public string? SnapshotJson { get; set; }
    }
}
