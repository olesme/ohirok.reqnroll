using Ohirok.Reporting.Config;

namespace Ohirok.Reporting.Model
{
    public class ReportRun
    {
        public string Name { get; set; } = "";
        public DateTimeOffset? StartedAt { get; set; }
        public DateTimeOffset? FinishedAt { get; set; }
        public double? DurationMs { get; set; }
        public ReportFolder Root { get; set; } = new ReportFolder { Name = "" };

        /// <summary>Resolved reporting.filters config, embedded client-side purely for legend labels/colors - matching itself already happened server-side into each ReportScenario.FilterMatches.</summary>
        public List<ReportFilterGroup> FilterGroups { get; set; } = [];

        /// <summary>The ReportHistoryConfigBase.HistoryWindow/HistoryDisplayWindow this run was generated with -
        /// null when no history backend is configured. report.js reads these once (there's
        /// one static config per report generation) to drive its client-side Health computation and to know how
        /// many of each scenario's over-fetched RecentRuns are within the real analysis window vs. just shipped
        /// for display.</summary>
        public int? HistoryAnalysisWindow { get; set; }
        public int? HistoryDisplayWindow { get; set; }
    }
}
