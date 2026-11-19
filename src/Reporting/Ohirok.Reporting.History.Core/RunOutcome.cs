namespace Ohirok.Reporting.History
{
    /// <summary>
    /// Ordinals 0-2 (Passed/Failed/Incomplete) are the original 3-bucket vocabulary and must never change -
    /// they're already persisted as raw ints in every SQL/NoSQL history backend. PassWithIssues/Skipped/
    /// Ambiguous ("W-full") were appended for display-only granularity: they share their
    /// exact names with the unrelated ReportStatus enum so report.js's existing statusColor()/STATUS_ICON_SVG
    /// lookups (keyed by these same names) render them with zero new CSS/JS. ReportHealthAnalyzer still
    /// buckets every value down to "real"/"pass-like" via IsReal/IsPassLike, so this widening cannot change
    /// any existing Health result.
    /// </summary>
    public enum RunOutcome
    {
        Passed = 0,
        Failed = 1,
        Incomplete = 2,
        PassWithIssues = 3,
        Skipped = 4,
        Ambiguous = 5
    }
}
