using NUnit.Framework;
using Ohirok.PersistentCleanup;
using PersistentCleanupSqliteNUnitTests.Bindings;
using PersistentCleanupSqliteNUnitTests.Support;

namespace PersistentCleanupSqliteNUnitTests.StepDefinitions
{
    [Binding]
    public class BasicCleanupSteps(ICleanupService cleanupManager, ScenarioContext scenarioContext)
    {
        [Given("I have a test resource {string}")]
        public void GivenTestResource(string resourceId)
        {
            scenarioContext[$"resource_{resourceId}"] = resourceId;
        }

        [Given("I have a failing resource {string}")]
        public void GivenFailingResource(string resourceId)
        {
            scenarioContext[$"failing_{resourceId}"] = resourceId;
        }

        [When("I register cleanup for {string}")]
        public async Task WhenRegisterCleanup(string resourceId)
        {
            var id = await cleanupManager.AddAsync<TestCleanups>(
                x => x.CleanupUser(resourceId),
                description: $"Cleanup {resourceId}");
            Remember(resourceId, id);
        }

        [When("I register cleanup for {string} with order {int}")]
        public async Task WhenRegisterCleanupWithOrder(string resourceId, int order)
        {
            var id = await cleanupManager.AddAsync<TestCleanups>(
                x => x.CleanupUser(resourceId),
                order: order);
            Remember(resourceId, id);
        }

        [When("I register cleanup for {string} with description {string}")]
        public async Task WhenRegisterCleanupWithDescription(string resourceId, string description)
        {
            var id = await cleanupManager.AddAsync<TestCleanups>(
                x => x.CleanupUser(resourceId),
                description: description);
            Remember(resourceId, id);
        }

        [When("I register cleanup for {string} with max retries {int}")]
        public async Task WhenRegisterCleanupWithRetries(string resourceId, int maxRetries)
        {
            var id = await cleanupManager.AddAsync<TestCleanups>(
                x => TestCleanups.CleanupFailingUser(resourceId),
                maxRetries: maxRetries);
            Remember(resourceId, id);
        }

        [When("I perform all cleanups")]
        public async Task WhenPerformAllCleanups()
        {
            var result = await cleanupManager.PerformAllAsync();
            scenarioContext["cleanup_result"] = result;
        }

        [Then("cleanup should have executed for {string}")]
        public static void ThenCleanupExecuted(string resourceId)
        {
            Assert.That(TestCleanups.WasExecuted(resourceId), Is.True,
                $"Cleanup for {resourceId} was not executed");
        }

        // Looks at the record that is really persisted in the SQLite file.
        // "Completed" means: the record is gone (persistentCleanup.deleteCompletedAfterDays is 0 in reqnroll.json).
        [Then("cleanup status should be {string}")]
        public void ThenCleanupStatus(string expectedStatus)
        {
            var id = (string)scenarioContext["lastCleanupId"];
            var actual = SqliteTestDb.ReadStatus(id);

            if (expectedStatus == "Completed")
                Assert.That(actual, Is.Null, "A completed record is deleted immediately when deleteCompletedAfterDays = 0");
            else
                Assert.That(actual, Is.EqualTo(Enum.Parse<CleanupStatus>(expectedStatus)));
        }

        [Then("cleanups should execute in order {string}")]
        public static void ThenExecutionOrder(string expectedOrder)
        {
            var expected = expectedOrder.Split(',');
            var actual = TestCleanups.GetExecutionOrder();
            Assert.That(actual, Is.EqualTo(expected));
        }

        [Then("cleanup should have been attempted {int} times")]
        public void ThenAttemptCount(int expectedAttempts)
        {
            var resourceId = scenarioContext.Keys
                .First(k => k.StartsWith("failing_"))
                .Replace("failing_", "");
            var actualAttempts = TestCleanups.GetAttemptCount(resourceId);
            Assert.That(actualAttempts, Is.EqualTo(expectedAttempts));
        }

        [Then("statistics should show {int} pending cleanups")]
        public async Task ThenPendingCount(int expected)
        {
            var stats = await cleanupManager.GetStatisticsAsync();
            Assert.That(stats.Pending, Is.EqualTo(expected));
        }

        [Then("statistics should show {int} failed cleanup")]
        public async Task ThenFailedCount(int expected)
        {
            var stats = await cleanupManager.GetStatisticsAsync();
            Assert.That(stats.Failed, Is.EqualTo(expected));
        }

        private void Remember(string resourceId, string cleanupId)
        {
            scenarioContext[$"cleanupId_{resourceId}"] = cleanupId;
            scenarioContext["lastCleanupId"] = cleanupId;
        }
    }
}
