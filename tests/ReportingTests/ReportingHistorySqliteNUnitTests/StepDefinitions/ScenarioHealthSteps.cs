using NUnit.Framework;
using Ohirok.Reporting.History;

namespace ReportingHistorySqliteNUnitTests.StepDefinitions
{
    [Binding]
    public class ScenarioHealthSteps(IReportHistoryStorage storage)
    {
        // Mirrors OhirokReportFormatter.HistoryWindowOverfetchFactor - keep in sync if that constant changes.
        private const int HistoryWindowOverfetchFactor = 5;

        private ScenarioHealth _health;

        [Given("scenario {string} has recorded outcomes {string} oldest first")]
        public async Task GivenScenarioHasRecordedOutcomes(string scenarioKey, string csvOutcomesOldestFirst)
        {
            var outcomes = csvOutcomesOldestFirst.Split(',').Select(o => Enum.Parse<RunOutcome>(o.Trim())).ToList();
            var baseTime = DateTimeOffset.UtcNow.AddMinutes(-outcomes.Count);

            for (var i = 0; i < outcomes.Count; i++)
            {
                await storage.SaveRunAsync(new ReportHistoryRunSnapshot
                {
                    RunAt = baseTime.AddSeconds(i),
                    Scenarios = { new ReportHistoryScenarioOutcome
                    {
                        ScenarioKey = scenarioKey,
                        ScenarioName = scenarioKey,
                        Outcome = outcomes[i]
                    } }
                });
            }
        }

        [When("I compute health for {string} with a window of {int}")]
        public async Task WhenIComputeHealth(string scenarioKey, int window)
        {
            var recentRuns = await storage.GetRecentRunsAsync(window * HistoryWindowOverfetchFactor);
            var recentOutcomes = recentRuns
                .SelectMany(r => r.Scenarios.Where(s => s.ScenarioKey == scenarioKey))
                .ToList();
            _health = ReportHealthAnalyzer.Analyze(recentOutcomes, window);
        }

        [Then("the health should be {string}")]
        public void ThenTheHealthShouldBe(string expected)
        {
            Assert.That(_health.ToString(), Is.EqualTo(expected));
        }
    }
}
