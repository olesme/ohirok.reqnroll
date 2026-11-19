using Ohirok.Reporting.Model;

namespace Ohirok.Reporting.Merging
{
    /// <summary>Shared depth-first scenario walk over a ReportRun's tree - factored out so
    /// ReportRunCombiner and ScenarioAttemptMerger don't each keep their own copy.</summary>
    internal static class ScenarioEnumerator
    {
        public static IEnumerable<ReportScenario> EnumerateScenarios(ReportRun run) => EnumerateFolder(run.Root);

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
