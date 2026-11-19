using Ohirok.Reporting.Config;
using Ohirok.Reporting.Model;
using Ohirok.Reporting.Parsing;

namespace Ohirok.Reporting.Merging
{
    /// <summary>
    /// Folds a base run plus however many rerun-of-failures passes into ONE logical
    /// ReportRun, matching scenarios by ScenarioKey across the separately-run ndjson files that
    /// produced each ReportRun in the chain. ScenarioKey is
    /// purely Gherkin-AST-derived (stable across processes as long as both ran against the same
    /// .feature content), but TestCaseStarted.Attempt restarts at 0 per process, so attempts must be
    /// renumbered on merge rather than concatenated as-is.
    /// </summary>
    public static class ScenarioAttemptMerger
    {
        public readonly struct MergeResult(ReportRun run, int mergedScenarioCount, IReadOnlyList<string> orphanedScenarioKeys)
        {
            public ReportRun Run { get; } = run;
            public int MergedScenarioCount { get; } = mergedScenarioCount;

            /// <summary>ScenarioKeys found in a later chain file but absent from the base run's tree -
            /// unexpected (a rerun should only be a subset of the base), reported so the caller can warn
            /// instead of silently dropping them.</summary>
            public IReadOnlyList<string> OrphanedScenarioKeys { get; } = orphanedScenarioKeys;
        }

        /// <summary>chainOldestFirst[0] is the base run; every subsequent entry is a rerun-of-failures
        /// pass layered on top of it, oldest to newest. A chain of length 1 is returned untouched (the
        /// existing single-ndjson-per-run behavior, zero regression risk).</summary>
        public static MergeResult MergeChain(IReadOnlyList<ReportRun> chainOldestFirst)
        {
            if (chainOldestFirst.Count == 0)
                throw new ArgumentException("Chain must have at least one run.", nameof(chainOldestFirst));

            var baseRun = chainOldestFirst[0];
            if (chainOldestFirst.Count == 1)
                return new MergeResult(baseRun, 0, Array.Empty<string>());

            var byScenarioKey = new Dictionary<string, ReportScenario>();
            foreach (var scenario in ScenarioEnumerator.EnumerateScenarios(baseRun))
                if (!string.IsNullOrEmpty(scenario.ScenarioKey))
                    byScenarioKey[scenario.ScenarioKey!] = scenario;

            var mergedCount = 0;
            var orphaned = new List<string>();
            var totalDurationMs = baseRun.DurationMs ?? 0;
            var finishedAt = baseRun.FinishedAt;

            for (var i = 1; i < chainOldestFirst.Count; i++)
            {
                var rerun = chainOldestFirst[i];
                totalDurationMs += rerun.DurationMs ?? 0;
                finishedAt = rerun.FinishedAt ?? finishedAt;

                foreach (var rerunScenario in ScenarioEnumerator.EnumerateScenarios(rerun))
                {
                    var key = rerunScenario.ScenarioKey;
                    if (string.IsNullOrEmpty(key)) continue;

                    // A rerun filtered to the failed tests still describes every scenario of the features it touched;
                    // the ones it did not execute carry no result and must not overwrite the base run's.
                    if (rerunScenario.Attempts.All(a => a.Status is ReportStatus.Unknown or ReportStatus.NotRun))
                        continue;

                    if (!byScenarioKey.TryGetValue(key!, out var baseScenario))
                    {
                        orphaned.Add(key!);
                        continue;
                    }

                    MergeAttempts(baseScenario, rerunScenario, rerunIndex: i);
                    mergedCount++;
                }
            }

            baseRun.FinishedAt = finishedAt;
            baseRun.DurationMs = totalDurationMs;

            return new MergeResult(baseRun, mergedCount, orphaned);
        }

        /// <summary>Appends rerunScenario's attempts onto baseScenario's, renumbering AttemptNumber to
        /// continue after the base's last - see the class doc comment for why a raw concat would be
        /// wrong. Issues/Hashtags/FilterMatches take the rerun's own values (last-chain-file-wins, not a
        /// union) since they describe the final attempt's own data, not a running total.</summary>
        private static void MergeAttempts(ReportScenario baseScenario, ReportScenario rerunScenario, int rerunIndex)
        {
            var nextAttemptNumber = baseScenario.Attempts.Count > 0
                ? baseScenario.Attempts[baseScenario.Attempts.Count - 1].AttemptNumber + 1
                : 1;

            foreach (var attempt in rerunScenario.Attempts)
            {
                attempt.AttemptNumber = nextAttemptNumber++;
                attempt.Rerun = rerunIndex;
                baseScenario.Attempts.Add(attempt);
            }

            baseScenario.Issues = rerunScenario.Issues;
            baseScenario.Hashtags = rerunScenario.Hashtags;
            baseScenario.Status = EnvelopeModelBuilder.DeriveScenarioStatus(
                baseScenario.FinalAttempt, baseScenario.Attempts.Count, baseScenario.Issues);
            baseScenario.FilterMatches = ReportFilterMatcher.Match(baseScenario, ReportingConfig.Filters);
        }
    }
}
