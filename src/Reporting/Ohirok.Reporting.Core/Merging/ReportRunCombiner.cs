using Io.Cucumber.Messages.Types;
using Ohirok.Reporting.History;
using Ohirok.Reporting.Model;
using Ohirok.Reporting.Parsing;
using Reqnroll.Formatters.PayloadProcessing;

namespace Ohirok.Reporting.Merging
{
    /// <summary>
    /// Combines several independently-generated Ohirok Cucumber Messages ".ndjson" sidecars into one
    /// <see cref="ReportProject"/>, each source becoming its own sibling <see cref="ReportRun"/> - runs are
    /// appended side by side, never deep-merged.
    /// </summary>
    /// <summary>One named source's merge outcome - a chain of length 1 (the common case, no rerun)
    /// always has MergedScenarioCount 0 and no orphans.</summary>
    public readonly struct RunMergeInfo
    {
        public RunMergeInfo(string name, int mergedScenarioCount, IReadOnlyList<string> orphanedScenarioKeys)
        {
            Name = name;
            MergedScenarioCount = mergedScenarioCount;
            OrphanedScenarioKeys = orphanedScenarioKeys;
        }

        public string Name { get; }
        public int MergedScenarioCount { get; }
        public IReadOnlyList<string> OrphanedScenarioKeys { get; }
    }

    public readonly struct CombineResult
    {
        public CombineResult(ReportProject project, IReadOnlyList<RunMergeInfo> mergeInfos)
        {
            Project = project;
            MergeInfos = mergeInfos;
        }

        public ReportProject Project { get; }
        public IReadOnlyList<RunMergeInfo> MergeInfos { get; }
    }

    public static class ReportRunCombiner
    {
        /// <summary>Each source is one logical run - NdjsonPaths is an ordered attempt-chain, oldest
        /// (the base run) first, with any further entries being rerun-of-failures passes merged onto it
        /// (see ScenarioAttemptMerger). A single-path source is the pre-existing, unchanged
        /// behavior.</summary>
        public static CombineResult Combine(IReadOnlyList<(string Name, IReadOnlyList<string> NdjsonPaths)> sources)
        {
            var project = new ReportProject();
            var mergeInfos = new List<RunMergeInfo>();

            foreach (var source in sources)
            {
                var chain = new List<ReportRun>();
                foreach (var ndjsonPath in source.NdjsonPaths)
                {
                    var envelopes = new List<Envelope>();
                    foreach (var line in File.ReadLines(ndjsonPath))
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        envelopes.Add(NdjsonSerializer.Deserialize(line));
                    }

                    chain.Add(EnvelopeModelBuilder.Build(envelopes));
                }

                var mergeResult = ScenarioAttemptMerger.MergeChain(chain);
                mergeResult.Run.Name = source.Name;
                project.Runs.Add(mergeResult.Run);
                mergeInfos.Add(new RunMergeInfo(source.Name, mergeResult.MergedScenarioCount, mergeResult.OrphanedScenarioKeys));
            }

            ApplyCrossRunHistory(project);

            return new CombineResult(project, mergeInfos);
        }

        /// <summary>
        /// Synthesizes each scenario's RecentRuns from the runs being combined here, keyed by ScenarioKey.
        /// Deliberately simpler than OhirokReportFormatter.ApplyHistoryHealth's storage-backed history: this is
        /// "history across the runs you combined", not lifetime history from an external backend - Combine is
        /// a stateless tool with no history storage of its own to query. Health is no longer computed here -
        /// report.js's analyzeScenarioHealth() derives it client-side from RecentRuns, same
        /// as for OhirokReportFormatter's own output.
        /// </summary>
        private static void ApplyCrossRunHistory(ReportProject project)
        {
            var config = ReportHistoryConfigHelper.GetConfig<ReportHistoryConfigBase>();
            var analysisWindow = config.HistoryWindow;
            var displayWindow = config.HistoryDisplayWindow ?? analysisWindow;
            var fetchWindow = Math.Max(analysisWindow, displayWindow);

            foreach (var run in project.Runs)
            {
                run.HistoryAnalysisWindow = analysisWindow;
                run.HistoryDisplayWindow = displayWindow;
            }

            var byScenarioKey = new Dictionary<string, List<(DateTimeOffset RunAt, string RunId, ReportScenario Scenario)>>();

            foreach (var run in project.Runs)
            {
                var runAt = run.StartedAt ?? DateTimeOffset.MinValue;
                foreach (var scenario in ScenarioEnumerator.EnumerateScenarios(run))
                {
                    var scenarioKey = scenario.ScenarioKey;
                    if (string.IsNullOrEmpty(scenarioKey)) continue;

                    if (!byScenarioKey.TryGetValue(scenarioKey, out var entries))
                        byScenarioKey[scenarioKey] = entries = new List<(DateTimeOffset RunAt, string RunId, ReportScenario Scenario)>();

                    entries.Add((runAt, run.Name, scenario));
                }
            }

            foreach (var entries in byScenarioKey.Values)
            {
                var newestFirst = entries.OrderByDescending(e => e.RunAt).ToList();
                var recentRuns = newestFirst
                    .Take(fetchWindow)
                    .Select(e => new ReportHistoryRunSummary { RunAt = e.RunAt, Outcome = MapOutcome(e.Scenario.Status).ToString(), RunId = e.RunId })
                    .ToList();

                foreach (var (_, _, scenario) in newestFirst)
                {
                    scenario.RecentRuns = recentRuns;
                }
            }
        }

        /// <summary>Mirrors OhirokReportFormatter.MapOutcome - duplicated locally since that method is private
        /// inside the Reqnroll-runtime-dependent ReqnrollPlugin project, unreachable from this stateless tool.</summary>
        private static RunOutcome MapOutcome(ReportStatus status)
        {
            switch (status)
            {
                case ReportStatus.Passed: return RunOutcome.Passed;
                case ReportStatus.PassWithIssues: return RunOutcome.PassWithIssues;
                case ReportStatus.Skipped: return RunOutcome.Skipped;
                case ReportStatus.Ambiguous: return RunOutcome.Ambiguous;
                case ReportStatus.Unknown:
                case ReportStatus.Undefined:
                case ReportStatus.Pending:
                case ReportStatus.NotRun:
                    return RunOutcome.Incomplete;
                default:
                    return RunOutcome.Failed;
            }
        }

    }
}
