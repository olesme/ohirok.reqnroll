using NUnit.Framework;
using Ohirok.PersistentCleanup;
using PersistentCleanupSqliteNUnitTests.Bindings;

namespace PersistentCleanupSqliteNUnitTests.StepDefinitions
{
    /// <summary>
    /// Scenarios running in parallel share one database and one cleanup service. Each scenario therefore performs
    /// only ITS OWN records, by id (PerformAsync). PerformAllAsync would also pick up - and claim - the records of
    /// scenarios that are still running, which is exactly what the plugin documentation warns against.
    /// </summary>
    [Binding]
    public class ParallelCleanupSteps(ICleanupService cleanupManager, ScenarioContext scenarioContext)
    {
        private readonly ICleanupService _cleanupManager = cleanupManager;
        private readonly ScenarioContext _scenarioContext = scenarioContext;
        private readonly List<(string Resource, string Id, int Order)> _registered = [];
        private readonly object _registeredLock = new();
        private string _scenarioId;

        [Scope(Tag = "parallel")]
        [Given("I have a unique test identifier")]
        public void GivenUniqueTestIdentifier()
        {
            // Create unique scenario ID for isolation in parallel execution
            _scenarioId = $"{_scenarioContext.ScenarioInfo.Title}_{Guid.NewGuid():N}";
            _scenarioContext["ScenarioId"] = _scenarioId;
        }

        [Scope(Tag = "parallel")]
        [Given("I have a test resource {string}")]
        public void GivenTestResource(string resourceId)
        {
            var qualifiedId = $"{_scenarioId}_{resourceId}";
            _scenarioContext[$"resource_{resourceId}"] = qualifiedId;
        }

        [Scope(Tag = "parallel")]
        [Given("I have a failing resource {string}")]
        public void GivenFailingResource(string resourceId)
        {
            var qualifiedId = $"{_scenarioId}_{resourceId}";
            _scenarioContext[$"failing_{resourceId}"] = qualifiedId;
        }

        [Scope(Tag = "parallel")]
        [When("I register cleanup for {string}")]
        public async Task WhenRegisterCleanup(string resourceId)
        {
            var qualifiedId = GetQualifiedId(resourceId);
            var id = await _cleanupManager.AddAsync<TestCleanups>(
                x => x.CleanupUser(qualifiedId),
                description: $"Cleanup {qualifiedId}");
            Register(resourceId, id, 10000);
        }

        [Scope(Tag = "parallel")]
        [When("I register cleanup for {string} with order {int}")]
        public async Task WhenRegisterCleanupWithOrder(string resourceId, int order)
        {
            var qualifiedId = GetQualifiedId(resourceId);
            var id = await _cleanupManager.AddAsync<TestCleanups>(
                x => x.CleanupUser(qualifiedId),
                order: order);
            Register(resourceId, id, order);
        }

        [Scope(Tag = "parallel")]
        [When("I register cleanup for {string} with max retries {int}")]
        public async Task WhenRegisterCleanupWithRetries(string resourceId, int maxRetries)
        {
            var qualifiedId = GetQualifiedId(resourceId);
            var id = await _cleanupManager.AddAsync<TestCleanups>(
                x => TestCleanups.CleanupFailingUser(qualifiedId),
                maxRetries: maxRetries);
            Register(resourceId, id, 10000);
        }

        [Scope(Tag = "parallel")]
        [When("I register {int} cleanups concurrently")]
        public async Task WhenRegisterConcurrentCleanups(int count)
        {
            var tasks = Enumerable.Range(1, count).Select(async i =>
            {
                var resourceId = $"{_scenarioId}_Concurrent{i}";
                var id = await _cleanupManager.AddAsync<TestCleanups>(
                    x => x.CleanupUser(resourceId),
                    description: $"Concurrent cleanup {i}");
                Register($"Concurrent{i}", id, 10000);
            });

            await Task.WhenAll(tasks);
            _scenarioContext["ConcurrentCount"] = count;
        }

        // "Perform all cleanups" of THIS scenario: its own ids, lowest order first.
        [Scope(Tag = "parallel")]
        [When("I perform all cleanups")]
        public async Task WhenPerformAllCleanups()
        {
            List<(string Resource, string Id, int Order)> mine;
            lock (_registeredLock)
                mine = [.. _registered.OrderBy(r => r.Order)];

            var total = new CleanupExecutionResult();
            foreach (var record in mine)
            {
                var result = await _cleanupManager.PerformAsync(record.Id);
                total.Total += result.Total;
                total.Successful += result.Successful;
                total.Failed += result.Failed;
                total.Orphans += result.Orphans;
                total.Skipped += result.Skipped;
            }

            _scenarioContext["cleanup_result"] = total;
        }

        [Scope(Tag = "parallel")]
        [Then("cleanup should have executed for {string}")]
        public void ThenCleanupExecuted(string resourceId)
        {
            var qualifiedId = GetQualifiedId(resourceId);
            Assert.That(TestCleanups.WasExecuted(qualifiedId), Is.True,
                $"Cleanup for {qualifiedId} was not executed");
        }

        [Scope(Tag = "parallel")]
        [Then("cleanups should execute in order {string}")]
        public void ThenExecutionOrder(string expectedOrder)
        {
            var expectedIds = expectedOrder.Split(',')
                .Select(GetQualifiedId)
                .ToArray();

            var scenarioExecutions = TestCleanups.GetExecutionOrder()
                .Where(id => id.StartsWith(_scenarioId))
                .ToList();

            Assert.That(scenarioExecutions, Is.EqualTo(expectedIds),
                $"Expected order: {string.Join(", ", expectedIds)}, " +
                $"Actual order: {string.Join(", ", scenarioExecutions)}");
        }

        [Scope(Tag = "parallel")]
        [Then("cleanup should have been attempted {int} times")]
        public void ThenAttemptCount(int expectedAttempts)
        {
            var resourceId = _scenarioContext.Keys
                .First(k => k.StartsWith("failing_"))
                .Replace("failing_", "");
            var qualifiedId = GetQualifiedId(resourceId);
            var actualAttempts = TestCleanups.GetAttemptCount(qualifiedId);
            Assert.That(actualAttempts, Is.EqualTo(expectedAttempts));
        }

        [Scope(Tag = "parallel")]
        [Then("only my scenario's cleanups should be processed")]
        public void ThenOnlyMyScenarioProcessed()
        {
            // Everything this scenario registered carries its unique id; nothing else is checked here because
            // other scenarios' records are, by design, never touched by this scenario's PerformAsync calls.
            var result = (CleanupExecutionResult)_scenarioContext["cleanup_result"];
            lock (_registeredLock)
                Assert.That(result.Total + result.Skipped, Is.EqualTo(_registered.Count));
        }

        [Scope(Tag = "parallel")]
        [Then("my scenario should show {int} pending cleanups")]
        public void ThenMyScenarioPendingCount(int expected)
        {
            if (expected == 0)
            {
                // After perform - verify all registered cleanups were executed
                var scenarioExecutions = TestCleanups.GetExecutionOrder()
                    .Where(id => id.StartsWith(_scenarioId))
                    .ToList();

                Assert.That(scenarioExecutions.Count, Is.EqualTo(_registered.Count),
                    $"Expected all {_registered.Count} registered cleanups to execute");
            }
            else
            {
                // Before perform - verify registration count
                Assert.That(_registered.Count, Is.EqualTo(expected),
                    $"Expected {expected} cleanups to be registered");
            }
        }

        [Scope(Tag = "parallel")]
        [Then("all {int} cleanups should be saved")]
        public void ThenAllCleanupsSaved(int expected)
        {
            lock (_registeredLock)
                Assert.That(_registered.Count, Is.EqualTo(expected),
                    $"Expected {expected} cleanups to be saved, but found {_registered.Count}");
        }

        [Scope(Tag = "parallel")]
        [Then("all {int} cleanups should have executed")]
        public void ThenAllCleanupsExecuted(int expected)
        {
            var scenarioExecutions = TestCleanups.GetExecutionOrder()
                .Where(id => id.StartsWith(_scenarioId))
                .ToList();

            Assert.That(scenarioExecutions.Count, Is.EqualTo(expected),
                $"Expected {expected} cleanups to execute, but {scenarioExecutions.Count} executed");
        }

        [Scope(Tag = "parallel")]
        [Then("my scenario should show {int} failed cleanup")]
        public void ThenMyScenarioFailedCount(int expected)
        {
            // The result of this scenario's own PerformAsync calls.
            var result = (CleanupExecutionResult)_scenarioContext["cleanup_result"];
            Assert.That(result.Failed, Is.EqualTo(expected), $"Expected {expected} failed cleanups");
        }

        private void Register(string resourceId, string cleanupId, int order)
        {
            lock (_registeredLock)
                _registered.Add((resourceId, cleanupId, order));
        }

        private string GetQualifiedId(string resourceId)
        {
            var key = $"resource_{resourceId}";
            if (_scenarioContext.TryGetValue<string>(key, out string value1))
                return value1;

            key = $"failing_{resourceId}";
            if (_scenarioContext.TryGetValue<string>(key, out string value))
                return value;

            // Fallback for dynamically created IDs
            return $"{_scenarioId}_{resourceId}";
        }
    }
}
