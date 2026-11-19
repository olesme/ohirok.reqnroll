using System.Text.Json;
using NUnit.Framework;
using Ohirok.CustomBindings;
using Ohirok.PersistentCleanup;
using PersistentCleanupCustomStorageNUnitTests.Bindings;
using PersistentCleanupCustomStorageNUnitTests.Support;
using Reqnroll.BoDi;

namespace PersistentCleanupCustomStorageNUnitTests.StepDefinitions
{
    [Binding]
    public class ScopeAndRecoverySteps(ScenarioContext scenarioContext)
    {
        private InMemoryCleanupStorage _storage = new();
        private readonly Dictionary<string, ICleanupService> _services = [];
        private readonly List<string> _registeredUsers = [];
        private CleanupExecutionResult? _lastResult;
        private CleanupExecutionResult[] _lastResults = [];

        [Given("an empty shared in-memory storage")]
        public void GivenEmptySharedStorage()
        {
            _storage = new InMemoryCleanupStorage();
            _services.Clear();
        }

        [Given("a {string} record for {string} last attempted {int} minutes ago")]
        public void GivenRecordLastAttempted(string status, string user, int minutesAgo)
        {
            _storage.Seed(NewRecord("CleanupUser", user, Enum.Parse<CleanupStatus>(status),
                DateTime.UtcNow.AddMinutes(-minutesAgo)));
        }

        [Given("a record for a removed cleanup method with argument {string}")]
        public void GivenRecordForRemovedMethod(string user)
        {
            _storage.Seed(NewRecord("MethodThatWasRemoved", user, CleanupStatus.Pending, null));
        }

        [When("the service with scope {string} registers a cleanup for {string}")]
        public async Task WhenServiceRegistersCleanup(string scope, string user)
        {
            await Service(scope).AddAsync<TestCleanups>(x => TestCleanups.CleanupUser(user));
        }

        [When("the service with scope {string} registers a failing cleanup for {string} with {int} retries")]
        public async Task WhenServiceRegistersFailingCleanup(string scope, string user, int retries)
        {
            await Service(scope).AddAsync<TestCleanups>(x => TestCleanups.CleanupFailingUser(user), maxRetries: retries);
        }

        [When("{int} cleanups are registered by the service with scope {string}")]
        public async Task WhenManyCleanupsRegistered(int count, string scope)
        {
            var service = Service(scope);
            for (var i = 1; i <= count; i++)
            {
                var user = $"Claim{i}";
                _registeredUsers.Add(user);
                await service.AddAsync<TestCleanups>(x => TestCleanups.CleanupUser(user));
            }
        }

        [When("the service with scope {string} performs all cleanups")]
        public async Task WhenServicePerformsAll(string scope)
        {
            _lastResult = await Service(scope).PerformAllAsync();
        }

        [When("two services with scope {string} perform all cleanups at the same moment")]
        public async Task WhenTwoServicesPerform(string scope)
        {
            // Two distinct service instances over the same storage = two test processes over one database.
            var a = NewService(scope);
            var b = NewService(scope);
            _lastResults = await Task.WhenAll(
                Task.Run(() => a.PerformAllAsync()),
                Task.Run(() => b.PerformAllAsync()));
        }

        [Then("the cleanup for {string} should have run")]
        public void ThenCleanupShouldHaveRun(string user) =>
            Assert.That(TestCleanups.WasExecuted(user), Is.True, $"Cleanup for {user} did not run");

        [Then("the cleanup for {string} should not have run")]
        public void ThenCleanupShouldNotHaveRun(string user) =>
            Assert.That(TestCleanups.WasExecuted(user), Is.False, $"Cleanup for {user} ran, but must not");

        [Then("the failing cleanup for {string} should have been attempted {int} times")]
        public void ThenFailingAttempts(string user, int expected) =>
            Assert.That(TestCleanups.GetAttemptCount(user), Is.EqualTo(expected));

        [Then("the record for {string} should have status {string}")]
        public void ThenRecordStatus(string user, string expectedStatus)
        {
            var record = _storage.Records.Single(r => r.ArgumentsJson!.Contains($"\"{user}\""));
            Assert.That(record.Status, Is.EqualTo(Enum.Parse<CleanupStatus>(expectedStatus)));
        }

        [Then("the result should show {int} successful and {int} skipped")]
        public void ThenResultSuccessfulAndSkipped(int successful, int skipped)
        {
            Assert.That(_lastResult, Is.Not.Null);
            Assert.That(_lastResult!.Successful, Is.EqualTo(successful));
            Assert.That(_lastResult.Skipped, Is.EqualTo(skipped));
        }

        [Then("the result should show {int} orphan")]
        public void ThenResultOrphans(int orphans)
        {
            Assert.That(_lastResult, Is.Not.Null);
            Assert.That(_lastResult!.Orphans, Is.EqualTo(orphans));
        }

        [Then("every registered cleanup should have run exactly once")]
        public void ThenEveryCleanupRanOnce()
        {
            var executions = TestCleanups.GetExecutionOrder();
            Assert.That(executions, Has.Count.EqualTo(_registeredUsers.Count), "Total number of executions");
            Assert.That(executions.GroupBy(x => x).Where(g => g.Count() != 1).Select(g => g.Key), Is.Empty,
                "Records that ran more than once");
            Assert.That(executions, Is.EquivalentTo(_registeredUsers));
        }

        [Then("the two results together should show {int} successful")]
        public void ThenTwoResultsTogether(int successful) =>
            Assert.That(_lastResults.Sum(r => r.Successful), Is.EqualTo(successful));

        // (TestCleanups.Reset() is already called by the BeforeScenario hook in CustomStorageSteps.)

        // ------------------------------------------------------------------------------------------------

        private ICleanupService Service(string scope)
        {
            if (!_services.TryGetValue(scope, out var service))
                _services[scope] = service = NewService(scope);
            return service;
        }

        private ICleanupService NewService(string scope)
        {
            var bindingProvider = scenarioContext.ScenarioContainer.Resolve<ICustomBindingProvider>();
            var container = scenarioContext.ScenarioContainer.Resolve<IObjectContainer>();
            var config = new CleanupConfigBase { Scope = scope, LogLevel = LogLevel.None, MaxConcurrency = 5 };
            return new CleanupService(bindingProvider, container, _storage, config);
        }

        private static CleanupRecord NewRecord(string methodKey, string user, CleanupStatus status, DateTime? lastAttempt) => new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Scope = string.Empty,
            TypeName = typeof(TestCleanups).FullName,
            MethodKey = methodKey,
            Signature = "System.String",
            ArgumentsJson = JsonSerializer.Serialize(new[] { user }),
            CreatedAt = DateTime.UtcNow.AddHours(-2),
            Status = status,
            LastAttemptAt = lastAttempt,
            MaxRetries = 3,
            Order = 10000
        };
    }
}
