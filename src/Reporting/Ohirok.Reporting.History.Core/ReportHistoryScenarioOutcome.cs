namespace Ohirok.Reporting.History
{
    public class ReportHistoryScenarioOutcome
    {
        public string ScenarioKey { get; set; } = "";
        public string ScenarioName { get; set; } = "";
        public RunOutcome Outcome { get; set; }

        /// <summary>Optional raw JSON payload for a historical scenario snapshot. The report client uses this as
        /// the source of truth when rendering a prior scenario inline; it is left null for history-only summary rows.</summary>
        public string? SnapshotJson { get; set; }
    }
}
