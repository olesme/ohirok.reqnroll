using Io.Cucumber.Messages.Types;
using NUnit.Framework;
using Ohirok.Reporting.Merging;
using Ohirok.Reporting.Model;
using Reqnroll.Formatters.PayloadProcessing;

namespace Ohirok.Reporting.Combine.Tests
{
    /// <summary>
    /// Base-run + rerun-of-failures merge. Builds two small synthetic ndjson files (a base run
    /// with one Passed and one Failed scenario, and a "rerun" containing only the previously-Failed
    /// scenario, now Passed) and drives them through <see cref="ReportRunCombiner.Combine"/> exactly as
    /// <c>Ohirok.Reporting.Combine</c>'s CLI would when given one --name with two chained --ndjson entries.
    /// </summary>
    [TestFixture]
    public class ScenarioAttemptMergerTests
    {
        private string _workDir = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ohirok-combine-merge-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_workDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        [Test]
        public void ChainOfTwo_MergesRerunScenarioOntoBase_RenumberingAttemptsAndEscalatingStatus()
        {
            const string featureUri = "merge/History.feature";
            const string passingScenario = "Always passes";
            const string flakyScenario = "Fails on first attempt";

            var basePath = Path.Combine(_workDir, "base.ndjson");
            var rerunPath = Path.Combine(_workDir, "rerun.ndjson");

            WriteNdjson(basePath, BuildEnvelopes(featureUri, new[]
            {
                (passingScenario, TestStepResultStatus.PASSED),
                (flakyScenario, TestStepResultStatus.FAILED),
            }));

            WriteNdjson(rerunPath, BuildEnvelopes(featureUri, new[]
            {
                (flakyScenario, TestStepResultStatus.PASSED),
            }));

            var sources = new List<(string Name, IReadOnlyList<string> NdjsonPaths)>
            {
                ("ci-run", new List<string> { basePath, rerunPath }),
            };

            var result = ReportRunCombiner.Combine(sources);

            Assert.That(result.Project.Runs.Count, Is.EqualTo(1), "Expected one logical Run node, not a sibling per ndjson file.");
            Assert.That(result.MergeInfos.Count, Is.EqualTo(1));
            Assert.That(result.MergeInfos[0].MergedScenarioCount, Is.EqualTo(1));
            Assert.That(result.MergeInfos[0].OrphanedScenarioKeys, Is.Empty);

            var scenarios = EnumerateScenarios(result.Project.Runs[0]).ToList();
            Assert.That(scenarios.Count, Is.EqualTo(2));

            var passing = scenarios.Single(s => s.Name == passingScenario);
            Assert.That(passing.Attempts.Count, Is.EqualTo(1), "Untouched scenario must keep exactly one attempt.");
            Assert.That(passing.Status, Is.EqualTo(ReportStatus.Passed));

            var flaky = scenarios.Single(s => s.Name == flakyScenario);
            Assert.That(flaky.Attempts.Count, Is.EqualTo(2), "Rerun's attempt must be appended onto the base scenario's attempts.");
            Assert.That(flaky.Attempts[0].AttemptNumber, Is.EqualTo(1));
            Assert.That(flaky.Attempts[1].AttemptNumber, Is.EqualTo(2), "Rerun's attempt must be renumbered to continue after the base's last attempt, not restart at its own process-local 1.");
            Assert.That(flaky.FinalAttempt!.Status, Is.EqualTo(ReportStatus.Passed));
            Assert.That(flaky.Status, Is.EqualTo(ReportStatus.PassWithIssues), "A scenario that failed then passed on rerun must escalate via DeriveScenarioStatus's attemptCount>1 rule.");
        }

        /// <summary>A rerun filtered to the failed tests still describes the other scenarios of their features, with no
        /// result. Those must leave the base run's result alone; the scenario that was rerun is marked as a rerun.</summary>
        [Test]
        public void RerunFilteredToFailedTests_LeavesScenariosItDidNotExecuteUntouched_AndMarksTheRerunAttempt()
        {
            const string featureUri = "merge/Filtered.feature";
            var basePath = Path.Combine(_workDir, "base.ndjson");
            var rerunPath = Path.Combine(_workDir, "rerun.ndjson");
            WriteNdjson(basePath, BuildEnvelopes(featureUri, new[] { ("Stable", TestStepResultStatus.PASSED), ("Flaky", TestStepResultStatus.FAILED) }));
            WriteNdjson(rerunPath, BuildEnvelopes(featureUri, new[] { ("Stable", TestStepResultStatus.UNKNOWN), ("Flaky", TestStepResultStatus.PASSED) }));

            var result = ReportRunCombiner.Combine([("ci-run", new List<string> { basePath, rerunPath })]);
            var scenarios = EnumerateScenarios(result.Project.Runs[0]).ToList();

            var stable = scenarios.Single(s => s.Name == "Stable");
            Assert.That(stable.Attempts.Count, Is.EqualTo(1), "A scenario the rerun did not execute must not get a rerun attempt.");
            Assert.That(stable.Status, Is.EqualTo(ReportStatus.Passed));

            var flaky = scenarios.Single(s => s.Name == "Flaky");
            Assert.That(flaky.Attempts.Select(a => a.Rerun), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(flaky.Status, Is.EqualTo(ReportStatus.PassWithIssues));
        }

        [Test]
        public void ChainOfOne_ReturnsRunUnchanged_FastPath()
        {
            const string featureUri = "merge/Single.feature";
            const string scenarioName = "Only scenario";

            var path = Path.Combine(_workDir, "single.ndjson");
            WriteNdjson(path, BuildEnvelopes(featureUri, new[] { (scenarioName, TestStepResultStatus.PASSED) }));

            var sources = new List<(string Name, IReadOnlyList<string> NdjsonPaths)>
            {
                ("single-run", new List<string> { path }),
            };

            var result = ReportRunCombiner.Combine(sources);

            Assert.That(result.Project.Runs.Count, Is.EqualTo(1));
            Assert.That(result.MergeInfos[0].MergedScenarioCount, Is.EqualTo(0));
            Assert.That(result.MergeInfos[0].OrphanedScenarioKeys, Is.Empty);

            var scenario = EnumerateScenarios(result.Project.Runs[0]).Single();
            Assert.That(scenario.Attempts.Count, Is.EqualTo(1));
        }

        [Test]
        public void RerunScenarioAbsentFromBase_IsReportedAsOrphaned_NotSynthesizedIntoTree()
        {
            const string baseFeatureUri = "merge/Base.feature";
            const string rerunFeatureUri = "merge/Rerun.feature";
            const string baseScenario = "Base scenario";
            const string unexpectedScenario = "Not part of the base run";

            var basePath = Path.Combine(_workDir, "base.ndjson");
            var rerunPath = Path.Combine(_workDir, "rerun.ndjson");

            WriteNdjson(basePath, BuildEnvelopes(baseFeatureUri, new[] { (baseScenario, TestStepResultStatus.PASSED) }));
            WriteNdjson(rerunPath, BuildEnvelopes(rerunFeatureUri, new[] { (unexpectedScenario, TestStepResultStatus.PASSED) }));

            var sources = new List<(string Name, IReadOnlyList<string> NdjsonPaths)>
            {
                ("ci-run", new List<string> { basePath, rerunPath }),
            };

            var result = ReportRunCombiner.Combine(sources);

            Assert.That(result.MergeInfos[0].MergedScenarioCount, Is.EqualTo(0));
            Assert.That(result.MergeInfos[0].OrphanedScenarioKeys.Count, Is.EqualTo(1));

            var scenarios = EnumerateScenarios(result.Project.Runs[0]).ToList();
            Assert.That(scenarios.Count, Is.EqualTo(1), "The orphaned rerun scenario must not be synthesized into the base run's tree.");
            Assert.That(scenarios[0].Name, Is.EqualTo(baseScenario));
        }

        private static void WriteNdjson(string path, IEnumerable<Envelope> envelopes) =>
            File.WriteAllLines(path, envelopes.Select(NdjsonSerializer.Serialize));

        private static IEnumerable<ReportScenario> EnumerateScenarios(ReportRun run) =>
            EnumerateFolder(run.Root);

        private static IEnumerable<ReportScenario> EnumerateFolder(ReportFolder folder)
        {
            foreach (var sub in folder.Subfolders)
                foreach (var s in EnumerateFolder(sub))
                    yield return s;

            foreach (var feature in folder.Features)
                foreach (var s in feature.Scenarios)
                    yield return s;
        }

        /// <summary>One feature with N independent single-step scenarios, each its own minimal but
        /// complete Cucumber Messages lifecycle - same minimal-envelope shape as
        /// FormatterEndToEndHistoryTests.BuildEnvelopes in ReportingHistoryFileSystemNUnitTests, just
        /// looped per scenario so ScenarioKey differs by name within one feature/run.</summary>
        private static List<Envelope> BuildEnvelopes(string featureUri, (string Name, TestStepResultStatus Status)[] scenarios)
        {
            var zero = new Timestamp(0, 0);
            var testRunStartedId = "trs-1";

            var featureChildren = new List<FeatureChild>();
            var scenarioAstIds = new List<string>();
            for (var i = 0; i < scenarios.Length; i++)
            {
                var scenarioId = $"scenario-{i}";
                var stepAstId = $"step-{i}";
                var step = new Step(new Location(2, null), "Given ", StepKeywordType.CONTEXT, "a step", null, null, stepAstId);
                var scenarioNode = new Scenario(new Location(1, null), new List<Tag>(), "Scenario", scenarios[i].Name, "",
                    new List<Step> { step }, new List<Examples>(), scenarioId);
                featureChildren.Add(new FeatureChild(null, null, scenarioNode));
                scenarioAstIds.Add(scenarioId);
            }

            var feature = new Feature(new Location(1, null), new List<Tag>(), "en", "Feature", "Merge test feature", "", featureChildren);
            var gherkinDocument = new GherkinDocument(featureUri, feature, new List<Comment>());

            var envelopes = new List<Envelope>
            {
                Envelope.Create(new TestRunStarted(zero, testRunStartedId)),
                Envelope.Create(gherkinDocument),
            };

            var overallSuccess = true;

            for (var i = 0; i < scenarios.Length; i++)
            {
                var (name, status) = scenarios[i];
                var scenarioAstId = scenarioAstIds[i];
                var pickleId = $"pickle-{i}";
                var pickleStepId = $"pickle-step-{i}";
                var testCaseId = $"tc-{i}";
                var testStepId = $"ts-{i}";
                var testCaseStartedId = $"tcs-{i}";
                var stepAstId = $"step-{i}";

                var pickleStep = new PickleStep(null, new List<string> { stepAstId }, pickleStepId, PickleStepType.CONTEXT, "a step");
                var pickle = new Pickle(pickleId, featureUri, name, "en",
                    new List<PickleStep> { pickleStep }, new List<PickleTag>(), new List<string> { scenarioAstId });

                var testStep = new TestStep(null, testStepId, pickleStepId, new List<string>(), new List<StepMatchArgumentsList>());
                var testCase = new TestCase(testCaseId, pickleId, new List<TestStep> { testStep }, testRunStartedId);

                var testCaseStarted = new TestCaseStarted(0, testCaseStartedId, testCaseId, null, zero);
                var testStepStarted = new TestStepStarted(testCaseStartedId, testStepId, zero);
                var testStepResult = new TestStepResult(new Duration(0, 0), null, status, null);
                var testStepFinished = new TestStepFinished(testCaseStartedId, testStepId, testStepResult, zero);
                var testCaseFinished = new TestCaseFinished(testCaseStartedId, zero, willBeRetried: false);

                if (status != TestStepResultStatus.PASSED) overallSuccess = false;

                envelopes.Add(Envelope.Create(pickle));
                envelopes.Add(Envelope.Create(testCase));
                envelopes.Add(Envelope.Create(testCaseStarted));
                envelopes.Add(Envelope.Create(testStepStarted));
                envelopes.Add(Envelope.Create(testStepFinished));
                envelopes.Add(Envelope.Create(testCaseFinished));
            }

            envelopes.Add(Envelope.Create(new TestRunFinished("", overallSuccess, zero, null, testRunStartedId)));

            return envelopes;
        }
    }
}
