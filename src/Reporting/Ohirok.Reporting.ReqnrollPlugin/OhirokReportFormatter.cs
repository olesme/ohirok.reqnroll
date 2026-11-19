using System.Text;
using System.Text.Json;
using Io.Cucumber.Messages.Types;
using Ohirok.PluginChains.Common;
using Ohirok.Reporting.Config;
using Ohirok.Reporting.History;
using Ohirok.Reporting.Model;
using Ohirok.Reporting.Parsing;
using Ohirok.Reporting.Rendering;
using Reqnroll.BoDi;
using Reqnroll.Formatters;
using Reqnroll.Formatters.Configuration;
using Reqnroll.Formatters.PayloadProcessing;
using Reqnroll.Formatters.RuntimeSupport;
using Reqnroll.Utils;

namespace Ohirok.Reporting.ReqnrollPlugin
{
    /// <summary>
    /// Writes a raw Cucumber Messages .ndjson sidecar while the run is in progress (same mechanism as Reqnroll's
    /// own built-in "message" formatter), then builds the report model and renders a standalone HTML file next to
    /// it once the run completes.
    /// </summary>
    public class OhirokReportFormatter : FileWritingFormatterBase
    {
        private readonly byte[] _newLineBytes = Encoding.UTF8.GetBytes(Environment.NewLine);
        private readonly List<Envelope> _envelopes = [];
        private readonly IReportHistoryStorage? _historyStorage;
        private string? _ndjsonPath;

        // IReportHistoryStorage is resolved here, at construction time, rather than lazily from the container
        // inside OnTargetFileStreamDisposing: BoDi's ObjectContainer.Dispose() flags itself disposed *before*
        // disposing its pooled objects (including this formatter), so any resolve attempted from within this
        // formatter's own Dispose path would always throw ObjectContainerException("Object container disposed").
        public OhirokReportFormatter(IFormattersConfigurationProvider configurationProvider, IFormatterLog logger, IFileSystem fileSystem, IObjectContainer container)
            : base(configurationProvider, logger, fileSystem, "ohirokReport", ".ndjson", "ohirok-report.ndjson")
        {
            container.TryResolve(out _historyStorage);
        }

        protected override void OnTargetFileStreamInitialized(Stream targetFileStream)
        {
            _ndjsonPath = (targetFileStream as FileStream)?.Name;
        }

        protected override async Task WriteToFile(Envelope envelope, CancellationToken cancellationToken)
        {
            _envelopes.Add(envelope);

            if (TargetFileStream != null)
            {
                await NdjsonSerializer.SerializeToStreamAsync(TargetFileStream, envelope);

                if (envelope.TestRunFinished == null)
                    await TargetFileStream.WriteAsync(_newLineBytes, 0, _newLineBytes.Length, cancellationToken);
            }
        }

        protected override void OnTargetFileStreamDisposing()
        {
            if (_ndjsonPath == null) return;

            var run = EnvelopeModelBuilder.Build(_envelopes);
            var title = ReportHtmlGenerator.SubstituteEnvVars(ReportingConfig.Title ?? string.Empty);
            run.Name = string.IsNullOrWhiteSpace(title) ? "Ohirok Report" : title;
            var htmlPath = Path.ChangeExtension(_ndjsonPath, ".html");
            var archiveDestinationPath = ApplyHistoryHealth(run, htmlPath);
            var project = new ReportProject { Runs = { run } };
            var html = ReportHtmlGenerator.Generate(project);

            File.WriteAllText(htmlPath, html, Encoding.UTF8);

            if (archiveDestinationPath != null)
            {
                try
                {
                    var archiveDir = Path.GetDirectoryName(archiveDestinationPath);
                    if (!string.IsNullOrEmpty(archiveDir)) Directory.CreateDirectory(archiveDir);
                    File.Copy(htmlPath, archiveDestinationPath, overwrite: true);
                }
                catch
                {
                    // Archiving is best-effort - a failed copy must never break report generation.
                }
            }
        }

        /// <summary>
        /// Raw run-snapshots fetched = fetchWindow * this factor, so Incomplete (skipped/not-invoked) runs
        /// interspersed above the analysis/display window don't silently shrink it below what's configured -
        /// GetRecentRunsAsync's count is a raw run count applied before any per-scenario filtering, so fetching
        /// exactly `fetchWindow` runs could hand ReportHealthAnalyzer/the display list fewer entries than
        /// intended for a scenario that was skipped in some of those runs. ReportHealthAnalyzer's own
        /// maxRealRuns cap and TrimToDisplayWindow's own cap then keep each list at its configured size
        /// regardless of this over-fetch. fetchWindow itself is Math.Max(analysisWindow, displayWindow), since
        /// the two windows can differ.
        /// </summary>
        private const int HistoryWindowOverfetchFactor = 5;

        /// <summary>
        /// Entirely optional (E6) - a report renders fine with no history backend installed. _historyStorage
        /// is only non-null when a Ohirok.Reporting.History.* plugin is present, so that being null is the
        /// common case, not an error. Calls are blocking because OnTargetFileStreamDisposing is sync (no async
        /// override exists on FileWritingFormatterBase).
        /// Returns the full path this run's HTML should be archived to (for the caller to copy htmlPath into,
        /// after it finishes writing it), or null when RunArchiveDirectory isn't configured/history is off.
        /// </summary>
        private string? ApplyHistoryHealth(ReportRun run, string htmlPath)
        {
            if (_historyStorage == null) return null;

            try
            {
                var storage = _historyStorage;
                var config = ReportHistoryConfigHelper.GetConfig<ReportHistoryConfigBase>();
                var analysisWindow = config.HistoryWindow;
                var displayWindow = config.HistoryDisplayWindow ?? analysisWindow;
                var fetchWindow = Math.Max(analysisWindow, displayWindow);
                var runAt = DateTimeOffset.UtcNow;

                run.HistoryAnalysisWindow = analysisWindow;
                run.HistoryDisplayWindow = displayWindow;

                // A configured PipelineRunId lets a CI pipeline pass the same value into both a base-run
                // process and its rerun-of-failures process, so their SaveRunAsync calls upsert into one
                // history row instead of two - see ReportHistorySnapshotMerger. ${ENV_VAR} references are
                // resolved like the title's, so one reqnroll.json can say "${GITHUB_RUN_ID}".
                var configuredRunId = ReportHtmlGenerator.SubstituteEnvVars(config.PipelineRunId ?? string.Empty);
                var runId = string.IsNullOrWhiteSpace(configuredRunId)
                    ? Guid.NewGuid().ToString("n")
                    : configuredRunId;

                // RunUrl is computed up front, before the archive copy actually happens (that happens back in
                // OnTargetFileStreamDisposing, after the HTML is rendered) - it's just a predictable filename,
                // not a feedback loop, so the snapshot below can carry its own final RunUrl from the start.
                string? runUrl = null;
                string? archiveDestinationPath = null;
                if (!string.IsNullOrWhiteSpace(config.RunArchiveDirectory))
                {
                    var htmlDir = Path.GetDirectoryName(Path.GetFullPath(htmlPath)) ?? ".";
                    var archiveDir = Path.GetFullPath(config.RunArchiveDirectory!);
                    archiveDestinationPath = Path.Combine(archiveDir, runId + ".html");
                    runUrl = GetRelativePath(htmlDir, archiveDestinationPath);
                }
                // A URL the pipeline publishes this run's report at survives where a local archive does not.
                var configuredRunUrl = ReportHtmlGenerator.SubstituteEnvVars(config.RunUrl ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(configuredRunUrl))
                    runUrl = configuredRunUrl;

                var scenarios = new List<ReportScenario>();
                var snapshot = new ReportHistoryRunSnapshot { RunAt = runAt, RunId = runId, RunUrl = runUrl };

                foreach (var scenario in EnumerateScenarios(run))
                {
                    var scenarioKey = scenario.ScenarioKey;
                    if (string.IsNullOrEmpty(scenarioKey)) continue;

                    // Not executed by this process (e.g. a rerun filtered to the failed tests still describes the
                    // other scenarios of their features): there is no result to record, and recording one would
                    // overwrite the base run's result when both share a pipelineRunId.
                    if (scenario.Status == ReportStatus.Unknown) continue;

                    scenarios.Add(scenario);
                    var lastStepWithError = scenario.Steps.LastOrDefault(s => !string.IsNullOrWhiteSpace(s.ErrorMessage) || !string.IsNullOrWhiteSpace(s.StackTrace));
                    var lastHookWithError = scenario.Hooks.LastOrDefault(h => !string.IsNullOrWhiteSpace(h.ErrorMessage) || !string.IsNullOrWhiteSpace(h.StackTrace));

                    snapshot.Scenarios.Add(new ReportHistoryScenarioOutcome
                    {
                        ScenarioKey = scenarioKey!,
                        ScenarioName = scenario.Name,
                        Outcome = MapOutcome(scenario.Status),
                        SnapshotJson = JsonSerializer.Serialize(new ReportHistoricalScenarioSnapshot
                        {
                            ScenarioKey = scenario.ScenarioKey,
                            Status = scenario.Status.ToString(),
                            Description = scenario.Description,
                            Comment = scenario.Comment,
                            ErrorMessage = lastStepWithError?.ErrorMessage ?? lastHookWithError?.ErrorMessage,
                            StackTrace = lastStepWithError?.StackTrace ?? lastHookWithError?.StackTrace,
                            ArgumentValues = scenario.ArgumentValues,
                            Steps = scenario.Steps,
                            Attachments = scenario.Attachments,
                            Hooks = scenario.Hooks,
                            Issues = scenario.Issues
                        })
                    });
                }

                if (snapshot.Scenarios.Count == 0) return null;

                // One save, one bulk read for the whole run - not one round-trip pair per scenario.
                storage.SaveRunAsync(snapshot).GetAwaiter().GetResult();
                var recentRuns = storage.GetRecentRunsAsync(fetchWindow * HistoryWindowOverfetchFactor).GetAwaiter().GetResult();

                var byScenarioKey = new Dictionary<string, List<HistoryEntry>>();
                foreach (var recentRun in recentRuns)
                {
                    foreach (var outcome in recentRun.Scenarios)
                    {
                        if (!byScenarioKey.TryGetValue(outcome.ScenarioKey, out var entries))
                            byScenarioKey[outcome.ScenarioKey] = entries = [];

                        entries.Add(new HistoryEntry(recentRun.RunAt, outcome.Outcome, recentRun.RunId, recentRun.RunUrl, outcome.SnapshotJson));
                    }
                }

                foreach (var scenario in scenarios)
                {
                    if (!byScenarioKey.TryGetValue(scenario.ScenarioKey!, out var entries)) continue;

                    // Health is no longer computed here - report.js's analyzeScenarioHealth() derives it
                    // client-side from scenario.RecentRuns (see feedback_client_side_computation memory).
                    scenario.RecentRuns = TrimToWindow(entries, fetchWindow);
                }

                return archiveDestinationPath;
            }
            catch
            {
                // A misbehaving history backend must never break report generation.
                return null;
            }
        }

        /// <summary>Path.GetRelativePath isn't available on netstandard2.0 (this project's target), so the
        /// relative path from one directory to another file is computed via Uri.MakeRelativeUri instead - the
        /// trailing separator on fromDirectory is required for Uri to treat it as a directory, not a file.</summary>
        private static string GetRelativePath(string fromDirectory, string toPath)
        {
            var fromUri = new Uri(AppendDirectorySeparator(fromDirectory));
            var toUri = new Uri(toPath);
            return Uri.UnescapeDataString(fromUri.MakeRelativeUri(toUri).ToString());
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        /// <summary>One scenario's outcome within one previously-persisted run snapshot, paired back up with
        /// that snapshot's RunAt after grouping GetRecentRunsAsync's results by ScenarioKey in memory.</summary>
        private readonly struct HistoryEntry(DateTimeOffset runAt, RunOutcome outcome, string? runId, string? runUrl, string? snapshotJson)
        {
            public DateTimeOffset RunAt { get; } = runAt;
            public RunOutcome Outcome { get; } = outcome;
            public string? RunId { get; } = runId;
            public string? RunUrl { get; } = runUrl;
            public string? SnapshotJson { get; } = snapshotJson;
        }

        /// <summary>Builds the *display* list (history dot strip, Analytics History matrix/trend chart, and
        /// the client-side Health computation in report.js). Keeps Incomplete runs
        /// (the "W-minimal" outcome) so they render as a visible outcome instead of silently vanishing. Capped at
        /// `window` = fetchWindow = Math.Max(analysisWindow, displayWindow), not just HistoryDisplayWindow -
        /// report.js needs the larger of the two since it now does its own Health analysis over analysisWindow
        /// from this same shipped list, instead of receiving a precomputed scenario.Health.</summary>
        private static List<ReportHistoryRunSummary> TrimToWindow(List<HistoryEntry> runsNewestFirst, int window)
        {
            var result = new List<ReportHistoryRunSummary>();
            foreach (var run in runsNewestFirst)
            {
                result.Add(new ReportHistoryRunSummary
                {
                    RunAt = run.RunAt,
                    Outcome = run.Outcome.ToString(),
                    RunId = run.RunId,
                    RunUrl = run.RunUrl,
                    SnapshotJson = run.SnapshotJson
                });
                if (result.Count >= window) break;
            }

            return result;
        }

        /// <summary>PassWithIssues/Skipped/Ambiguous map to their own distinct RunOutcome values (the "W-full"
        /// outcome granularity) rather than folding into Passed/Incomplete as before - see RunOutcome's own doc
        /// comment for why this is safe for every already-persisted history row and for ReportHealthAnalyzer.
        /// Unknown/Undefined/Pending/NotRun remain Incomplete - genuine "never produced a verdict" states with
        /// no widened equivalent.</summary>
        private static RunOutcome MapOutcome(ReportStatus status)
        {
            return status switch
            {
                ReportStatus.Passed => RunOutcome.Passed,
                ReportStatus.PassWithIssues => RunOutcome.PassWithIssues,
                ReportStatus.Skipped => RunOutcome.Skipped,
                ReportStatus.Ambiguous => RunOutcome.Ambiguous,
                ReportStatus.Unknown or ReportStatus.Undefined or ReportStatus.Pending or ReportStatus.NotRun => RunOutcome.Incomplete,
                _ => RunOutcome.Failed,
            };
        }

        private static IEnumerable<ReportScenario> EnumerateScenarios(ReportRun run) => EnumerateFolder(run.Root);

        private static IEnumerable<ReportScenario> EnumerateFolder(ReportFolder folder)
        {
            foreach (var sub in folder.Subfolders)
                foreach (var s in EnumerateFolder(sub))
                    yield return s;

            foreach (var feature in folder.Features)
                foreach (var s in EnumerateFeature(feature))
                    yield return s;
        }

        private static IEnumerable<ReportScenario> EnumerateFeature(ReportFeature feature)
        {
            foreach (var s in feature.Scenarios)
                yield return s;

            foreach (var outline in feature.ScenarioOutlines)
                foreach (var s in EnumerateOutline(outline))
                    yield return s;

            foreach (var rule in feature.Rules)
                foreach (var s in EnumerateRule(rule))
                    yield return s;
        }

        private static IEnumerable<ReportScenario> EnumerateRule(ReportRule rule)
        {
            foreach (var s in rule.Scenarios)
                yield return s;

            foreach (var outline in rule.ScenarioOutlines)
                foreach (var s in EnumerateOutline(outline))
                    yield return s;
        }

        private static IEnumerable<ReportScenario> EnumerateOutline(ReportScenarioOutline outline)
        {
            foreach (var set in outline.ExampleSets)
                foreach (var s in set.Scenarios)
                    yield return s;
        }
    }
}
