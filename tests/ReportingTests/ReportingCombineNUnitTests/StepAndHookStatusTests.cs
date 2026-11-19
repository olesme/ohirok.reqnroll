using Io.Cucumber.Messages.Types;
using NUnit.Framework;
using Ohirok.Reporting.Merging;
using Ohirok.Reporting.Model;
using Reqnroll.Formatters.PayloadProcessing;

namespace Ohirok.Reporting.Combine.Tests
{
    /// <summary>
    /// A scenario's status must agree with what the test runner reports: a failing step a plugin generated (bound to a
    /// step definition but to no Gherkin step, like SoftAssert's end-of-scenario flush) and a failing hook both fail it.
    /// </summary>
    [TestFixture]
    public class StepAndHookStatusTests
    {
        private string _workDir = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ohirok-status-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_workDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        [Test]
        public void FailingGeneratedStep_IsShownAsAStep_AndFailsTheScenario()
        {
            var scenario = Load(extra: Extra.GeneratedStep);

            Assert.That(scenario.Status, Is.EqualTo(ReportStatus.Failed));
            var steps = scenario.FinalAttempt!.Steps;
            Assert.That(steps.Count, Is.EqualTo(2), "The generated step must not be dropped.");
            Assert.That(steps[1].Keyword, Is.EqualTo("*"));
            Assert.That(steps[1].Text, Is.EqualTo("the collected soft assertion failures are reported"));
            Assert.That(steps[1].Status, Is.EqualTo(ReportStatus.Failed));
            Assert.That(steps[1].ErrorMessage, Does.Contain("2 soft assertions failed"));
        }

        [Test]
        public void FailingAfterScenarioHook_FailsTheScenario_EvenWhenEveryStepPassed()
        {
            var scenario = Load(extra: Extra.FailingHook);

            Assert.That(scenario.FinalAttempt!.Steps.Single().Status, Is.EqualTo(ReportStatus.Passed));
            Assert.That(scenario.FinalAttempt.Hooks.Single().Status, Is.EqualTo(ReportStatus.Failed));
            Assert.That(scenario.Status, Is.EqualTo(ReportStatus.Failed), "The runner reports this test as failed; the report must not say otherwise.");
        }

        private enum Extra { GeneratedStep, FailingHook }

        private ReportScenario Load(Extra extra)
        {
            var path = Path.Combine(_workDir, "run.ndjson");
            File.WriteAllLines(path, BuildEnvelopes(extra).Select(NdjsonSerializer.Serialize));
            var result = ReportRunCombiner.Combine([("run", new List<string> { path })]);
            return result.Project.Runs[0].Root.Subfolders.Count > 0
                ? result.Project.Runs[0].Root.Subfolders.SelectMany(f => f.Features).Single().Scenarios.Single()
                : result.Project.Runs[0].Root.Features.Single().Scenarios.Single();
        }

        private static List<Envelope> BuildEnvelopes(Extra extra)
        {
            const string uri = "status/Status.feature";
            var zero = new Timestamp(0, 0);
            var step = new Step(new Location(2, null), "Given ", StepKeywordType.CONTEXT, "a step", null, null, "step-0");
            var scenarioNode = new Scenario(new Location(1, null), [], "Scenario", "A scenario", "", [step], [], "scenario-0");
            var feature = new Feature(new Location(1, null), [], "en", "Feature", "Status feature", "", [new FeatureChild(null, null, scenarioNode)]);

            var pickleStep = new PickleStep(null, ["step-0"], "pickle-step-0", PickleStepType.CONTEXT, "a step");
            var pickle = new Pickle("pickle-0", uri, "A scenario", "en", [pickleStep], [], ["scenario-0"]);
            var gherkinStep = new TestStep(null, "ts-step", "pickle-step-0", [], []);

            var envelopes = new List<Envelope>
            {
                Envelope.Create(new TestRunStarted(zero, "trs")),
                Envelope.Create(new GherkinDocument(uri, feature, [])),
                Envelope.Create(pickle),
            };

            TestStep extraStep;
            string extraError;
            if (extra == Extra.GeneratedStep)
            {
                envelopes.Add(Envelope.Create(new StepDefinition("sd-flush",
                    new StepDefinitionPattern("the collected soft assertion failures are reported", StepDefinitionPatternType.CUCUMBER_EXPRESSION),
                    new SourceReference(null, new JavaMethod("SoftFlushStep", "Flush", []), null, null))));
                extraStep = new TestStep(null, "ts-extra", "", ["sd-flush"], [new StepMatchArgumentsList([])]);
                extraError = "[SOFT ASSERTIONS FLUSH] 2 soft assertions failed";
            }
            else
            {
                envelopes.Add(Envelope.Create(new Hook("hook-cleanup", null,
                    new SourceReference(null, new JavaMethod("Hooks", "CleanUp", []), null, null), null, HookType.AFTER_TEST_CASE)));
                extraStep = new TestStep("hook-cleanup", "ts-extra", null, null, null);
                extraError = "Could not delete the test customer";
            }

            envelopes.Add(Envelope.Create(new TestCase("tc", "pickle-0", [gherkinStep, extraStep], "trs")));
            envelopes.Add(Envelope.Create(new TestCaseStarted(0, "tcs", "tc", null, zero)));
            foreach (var (id, status, message) in new[] { ("ts-step", TestStepResultStatus.PASSED, (string?)null), ("ts-extra", TestStepResultStatus.FAILED, extraError) })
            {
                envelopes.Add(Envelope.Create(new TestStepStarted("tcs", id, zero)));
                envelopes.Add(Envelope.Create(new TestStepFinished("tcs", id, new TestStepResult(new Duration(0, 0), message, status, null), zero)));
            }
            envelopes.Add(Envelope.Create(new TestCaseFinished("tcs", zero, willBeRetried: false)));
            envelopes.Add(Envelope.Create(new TestRunFinished("", false, zero, null, "trs")));
            return envelopes;
        }
    }
}
