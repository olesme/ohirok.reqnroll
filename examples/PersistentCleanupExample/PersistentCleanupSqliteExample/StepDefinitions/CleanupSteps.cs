using NUnit.Framework;
using Ohirok.PersistentCleanup;
using PersistentCleanupSqliteExample.Bindings;

namespace PersistentCleanupSqliteExample.StepDefinitions
{
    [Binding]
    public class CleanupSteps(ICleanupService cleanupService, ScenarioContext scenarioContext)
    {
        [Given("a cleanup action {string} is registered with order {int}")]
        public async Task GivenCleanupRegisteredWithOrder(string key, int order)
        {
            await cleanupService.AddAsync<CleanupActions>(
                x => x.DeleteTempUser(key),
                order: order);
        }

        [Given("a cleanup action {string} is registered that always fails, with max retries {int}")]
        public async Task GivenFailingCleanupRegistered(string key, int maxRetries)
        {
            await cleanupService.AddAsync<CleanupActions>(
                x => CleanupActions.DeleteUndeletableResource(key),
                maxRetries: maxRetries);
        }

        [When("all pending cleanups are performed")]
        public async Task WhenAllPendingCleanupsArePerformed()
        {
            scenarioContext["cleanup_result"] = await cleanupService.PerformAllAsync();
        }

        [Then("the cleanup actions should have executed in order {string}")]
        public static void ThenExecutionOrder(string expectedOrder)
        {
            Assert.That(CleanupActions.ExecutionOrder, Is.EqualTo(expectedOrder.Split(',')));
        }

        [Then("the cleanup action {string} should have been attempted {int} time\\(s\\)")]
        public static void ThenAttemptCount(string key, int expectedAttempts)
        {
            Assert.That(CleanupActions.GetAttemptCount(key), Is.EqualTo(expectedAttempts));
        }

        [Then("the cleanup statistics should report {int} pending cleanups")]
        public async Task ThenPendingCount(int expected)
        {
            var stats = await cleanupService.GetStatisticsAsync();
            Assert.That(stats.Pending, Is.EqualTo(expected));
        }

        [Then("the cleanup statistics should report {int} failed cleanup")]
        public async Task ThenFailedCount(int expected)
        {
            var stats = await cleanupService.GetStatisticsAsync();
            Assert.That(stats.Failed, Is.EqualTo(expected));
        }
    }
}
