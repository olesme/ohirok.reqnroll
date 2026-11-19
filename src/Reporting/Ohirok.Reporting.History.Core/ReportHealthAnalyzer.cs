namespace Ohirok.Reporting.History
{
    // Pattern rules (Flaky/Regressed/Broken/Fixed/New/Invalid) adapted under MIT attribution from
    // Expressium.LivingDoc's MergeScenarioHistoryHealth algorithm (MIT License, Copyright Expressium),
    // with one behavioral change: Incomplete runs are excluded from the comparison window entirely
    // rather than counted as a distinct outcome.
    public static class ReportHealthAnalyzer
    {
        /// <param name="runsNewestFirst">Recent runs for one scenario, ordered newest first.</param>
        /// <param name="maxRealRuns">
        /// Caps how many non-Incomplete runs are considered, so a caller that over-fetches raw rows (to
        /// compensate for Incomplete runs interspersed above the configured comparison window - see
        /// OhirokReportFormatter.ApplyHistoryHealth) doesn't widen the comparison window as a side effect.
        /// Null means no cap (use every genuine run passed in).
        /// </param>
        public static ScenarioHealth Analyze(IReadOnlyList<ReportHistoryScenarioOutcome> runsNewestFirst, int? maxRealRuns = null)
        {
            if (runsNewestFirst == null || runsNewestFirst.Count == 0)
                return ScenarioHealth.Unknown;

            if (!IsReal(runsNewestFirst[0].Outcome))
                return ScenarioHealth.Invalid;

            var real = new List<ReportHistoryScenarioOutcome>();
            foreach (var run in runsNewestFirst)
            {
                if (IsReal(run.Outcome))
                {
                    real.Add(run);
                    if (maxRealRuns.HasValue && real.Count >= maxRealRuns.Value)
                        break;
                }
            }

            if (real.Count == 1)
                return ScenarioHealth.New;

            var transitions = 0;
            for (var i = 0; i < real.Count - 1; i++)
            {
                if (IsPassLike(real[i].Outcome) != IsPassLike(real[i + 1].Outcome))
                    transitions++;
            }

            if (transitions >= 2)
                return ScenarioHealth.Flaky;

            var latestPassLike = IsPassLike(real[0].Outcome);
            var previousPassLike = IsPassLike(real[1].Outcome);

            if (latestPassLike && previousPassLike) return ScenarioHealth.Passing;
            if (!latestPassLike && previousPassLike) return ScenarioHealth.Regressed;
            if (!latestPassLike && !previousPassLike) return ScenarioHealth.Broken;
            if (latestPassLike && !previousPassLike) return ScenarioHealth.Fixed;

            return ScenarioHealth.Unknown;
        }

        /// <summary>Excludes Incomplete (never finished) and, since the "W-full" outcome widening, Skipped/
        /// Ambiguous - none of these represent a genuine pass/fail verdict to compare against neighbors.</summary>
        private static bool IsReal(RunOutcome outcome) =>
            outcome != RunOutcome.Incomplete && outcome != RunOutcome.Skipped && outcome != RunOutcome.Ambiguous;

        /// <summary>Pass-like bucket for transition counting - Passed and PassWithIssues both count as "the
        /// scenario came out green", matching the pre-widening behavior where PassWithIssues didn't exist
        /// as a distinct value and was folded into Passed.</summary>
        private static bool IsPassLike(RunOutcome outcome) =>
            outcome == RunOutcome.Passed || outcome == RunOutcome.PassWithIssues;
    }
}
