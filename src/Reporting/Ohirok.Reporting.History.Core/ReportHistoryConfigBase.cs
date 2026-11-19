using Ohirok.PluginChains.Common;

namespace Ohirok.Reporting.History
{
    public class ReportHistoryConfigBase
    {
        public bool Enabled { get; set; } = true;
        public bool AutoCreateDb { get; set; } = true;
        public int HistoryWindow { get; set; } = 4;

        /// <summary>How many prior runs to display (history dot strip, Analytics History matrix/trend
        /// chart) - distinct from HistoryWindow, which is the analysis window used to derive Health.
        /// Null (default) falls back to HistoryWindow, preserving the one-number-for-both behavior.</summary>
        public int? HistoryDisplayWindow { get; set; }

        /// <summary>Directory each run's full HTML report gets copied into (as "&lt;RunId&gt;.html"), enabling
        /// click-through from a history dot to that exact archived run. Resolved the same way as a storage
        /// backend's own file path (relative paths are relative to the process working directory) - and for
        /// click-through links to keep working across CI runs, this directory must sit at the same relative
        /// offset from wherever the live report is generated every run, same assumption CI already makes about
        /// the history file/database staying put. Null (default) - the feature is off, history dots render
        /// plain and non-clickable, matching every other optional history feature's off-by-default convention.</summary>
        public string? RunArchiveDirectory { get; set; }

        /// <summary>Stable logical run id shared across a base run and its rerun-of-failures pass (Option A) -
        /// set this to the SAME literal value (e.g. a CI build/run number) in both processes'
        /// config so their SaveRunAsync calls upsert into one history row (scenarios merged by ScenarioKey,
        /// see <see cref="ReportHistorySnapshotMerger"/>) instead of producing two sibling rows for what
        /// Combine already renders as one logical run. Null (default) - every run generates its own random
        /// RunId as before, matching every prior release's behavior.</summary>
        public string? PipelineRunId { get; set; }

        /// <summary>Where this run's full report will be published (an absolute URL or a path relative to later
        /// reports), recorded in history so the history views link to it. <c>${ENV_VAR}</c> references are resolved,
        /// e.g. <c>"https://example.org/reports/${GITHUB_RUN_ID}/ohirok-report.html"</c>. Takes precedence over the
        /// relative link <see cref="RunArchiveDirectory"/> produces. Null with no archive directory: history has no
        /// links to full reports, and the report shows none.</summary>
        public string? RunUrl { get; set; }
    }

    public static class ReportHistoryConfigHelper
    {
        public static TConfig GetConfig<TConfig>(string sectionName = "reportingHistory")
            where TConfig : ReportHistoryConfigBase, new()
        {
            return PluginConfigProvider.GetSection<TConfig>(sectionName);
        }
    }
}
