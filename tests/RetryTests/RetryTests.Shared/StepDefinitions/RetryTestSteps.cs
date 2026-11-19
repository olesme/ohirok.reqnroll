using System.Collections.Concurrent;
using Ohirok.Retry;
using Ohirok.Retry.Extensions;
using RetryTests.Support;

namespace RetryTests.StepDefinitions
{
    [Binding]
    public sealed class RetryTestSteps(ScenarioContext sc, ITestRunner runner, IReqnrollOutputHelper output)
    {
        private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> TestStorage = new();

        private string ScenarioId => TestIdHelper.GetScenarioId(sc);

        private ConcurrentDictionary<string, object> GetStorage(string testId) =>
            TestStorage.GetOrAdd($"{ScenarioId}:{testId}", _ => new ConcurrentDictionary<string, object>());

        private int IncrementAndGet(string testId, string key) =>
            (int)GetStorage(testId).AddOrUpdate(key, 1, (_, v) => (int)v + 1);

        private int GetCount(string testId, string key) =>
            GetStorage(testId).TryGetValue(key, out var v) ? (int)v : 0;

        [BeforeScenario(Order = int.MinValue + 1)]
        public void ClearTestStorage()
        {
            if (Manager.GetRetryContext(runner)?.Attempt > 1) return;

            var prefix = ScenarioId + ":";
            foreach (var key in TestStorage.Keys)
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    TestStorage.TryRemove(key, out _);
        }

        [Given("test storage is initialized")]
        public void TestStorageInitialized() { }

        [When("success")]
        [Then("success")]
        public void Success() => output.WriteLine("Success");

        [Given("counter is incremented <{word}>")]
        public void CounterIsIncremented(string testId) =>
            GetStorage(testId).AddOrUpdate("counter", 1, (_, v) => (int)v + 1);

        [When("counter is set to {int} <{word}>")]
        public void CounterIsSetTo(int value, string testId) =>
            GetStorage(testId)["counter"] = value;

        [Then("counter should be {int} <{word}>")]
        public void CounterShouldBe(int expected, string testId)
        {
            var actual = GetCount(testId, "counter");
            actual.ShouldBe(expected, $"Counter mismatch for {testId}");
        }

        [When("operation fails {int} time(s) <{word}>")]
        public void OperationFails(int times, string testId)
        {
            int attempt = IncrementAndGet(testId, "attempts");
            if (attempt <= times)
                throw new InvalidOperationException($"Intentional failure on attempt {attempt}");
        }

        [When("operation sleeps {int}ms and fails {int} time(s) <{word}>")]
        public async Task OperationSleepsAndFails(int ms, int times, string testId)
        {
            await Task.Delay(ms);
            OperationFails(times, testId);
        }

        [When("operation always fails <{word}>")]
        public void OperationAlwaysFails(string testId)
        {
            IncrementAndGet(testId, "attempts");
            throw new InvalidOperationException("Always fails");
        }

        [When("operation sleeps {int}ms and always fails <{word}>")]
        public async Task OperationSleepsAndAlwaysFails(int ms, string testId)
        {
            await Task.Delay(ms);
            OperationAlwaysFails(testId);
        }

        [When("operation fails at {int} attempt <{word}>")]
        public void OperationFailsAtAttempt(int attemptNumber, string testId)
        {
            var ctx = sc.GetRetryContext();
            int current = ctx?.Attempt ?? IncrementAndGet(testId, "atAttempt");
            if (current == attemptNumber)
                throw new InvalidOperationException($"Intentional failure at attempt {attemptNumber}");
        }

        [When("operation throws {word} <{word}>")]
        public void OperationThrows(string exType, string testId)
        {
            int attempt = IncrementAndGet(testId, "attempts");
            if (attempt <= 2)
            {
                throw exType.ToLowerInvariant() switch
                {
                    "timeout" => (Exception)new TimeoutException("Simulated timeout"),
                    "invalid" => new InvalidOperationException("Simulated invalid"),
                    "custom" => new CustomTestException("Simulated custom"),
                    _ => new ArgumentException($"Unknown exception type: {exType}")
                };
            }
        }

        [When("operation throws timeout or invalid <{word}>")]
        public void OperationThrowsTimeoutOrInvalid(string testId)
        {
            int attempt = IncrementAndGet(testId, "attempts");
            if (attempt == 1)
                throw new TimeoutException("Simulated timeout on attempt 1");
            if (attempt == 2)
                throw new InvalidOperationException("Simulated invalid on attempt 2");
        }

        [When("operation throws custom exception <{word}>")]
        public void OperationThrowsCustom(string testId)
        {
            int attempt = IncrementAndGet(testId, "attempts");
            if (attempt <= 2)
                throw new CustomTestException("Simulated custom");
        }

        [Then("verify attempt count is {int} <{word}>")]
        public void VerifyAttemptCount(int expected, string testId)
        {
            int actual = GetCount(testId, "attempts");
            actual.ShouldBe(expected, $"Attempt count mismatch for {testId}");
        }

        [Then("verify step succeeded <{word}>")]
        public void VerifyStepSucceeded(string testId)
        {
            int actual = GetCount(testId, "attempts");
            actual.ShouldBeGreaterThan(1, $"Expected more than 1 attempt for {testId}");
        }

        [Then("verify both steps succeeded <{word}> <{word}>")]
        public void VerifyBothStepsSucceeded(string testId1, string testId2)
        {
            VerifyStepSucceeded(testId1);
            VerifyStepSucceeded(testId2);
        }

        [Then("verify both blocks succeeded <{word}> <{word}>")]
        public void VerifyBothBlocksSucceeded(string testId1, string testId2) =>
            VerifyBothStepsSucceeded(testId1, testId2);

        [Then("verify all blocks succeeded <{word}> <{word}> <{word}>")]
        public void VerifyAllBlocksSucceeded(string t1, string t2, string t3)
        {
            VerifyStepSucceeded(t1);
            VerifyStepSucceeded(t2);
            VerifyStepSucceeded(t3);
        }

        [Then("the {word} attempt number is {int}")]
        public void TheAttemptNumberIs(string type, int expected)
        {
            var ctx = sc.GetRetryContext();
            ctx.ShouldNotBeNull($"Expected active {type} retry context");
            ctx.Attempt.ShouldBe(expected);
        }

        [Then("verify delay was at least {int}ms <{word}>")]
        public void VerifyDelayWasAtLeast(int minMs, string testId)
        {
            var ctx = sc.GetRetryContext();
            ctx.ShouldNotBeNull("Expected active retry context for delay check");
            ctx.Elapsed.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(minMs,
                "Total elapsed time should include all inter-attempt delays");
        }

        [When("{string} key added to scenario context on {int} attempt")]
        public void KeyAddedToScenarioContextOnAttempt(string key, int attemptNumber)
        {
            var ctx = sc.GetRetryContext();
            int current = ctx?.Attempt ?? 1;
            if (current == attemptNumber)
                sc[key] = $"{attemptNumber} attempt";
        }

        [Then("{string} key persists in scenario context with value {string}")]
        public void KeyPersistsInScenarioContext(string key, string expected)
        {
            sc.ContainsKey(key).ShouldBeTrue($"Key '{key}' not found in ScenarioContext");
            sc[key]?.ToString().ShouldBe(expected);
        }

        [Then("{word} hook fires {int} time(s)")]
        public void HookFiresNTimes(string hookKey, int expected)
        {
            int actual = sc.ContainsKey(hookKey) ? (int)sc[hookKey] : 0;
            actual.ShouldBe(expected, $"Hook '{hookKey}' fire count mismatch");
        }

        [Then("verify hook saw {int} attempts <{word}>")]
        public void VerifyHookSawAttempts(int expected, string testId)
        {
            var count = sc.TryGetValue("FinalAttemptCount", out object? value) ? (int)value : 0;
            count.ShouldBe(expected);
        }

        [Then("verify hook accessed context <{word}>")]
        public void VerifyHookAccessedContext(string testId)
        {
            var accessed = sc.TryGetValue("HookAccessedContext", out object? value) && (bool)value;
            accessed.ShouldBeTrue("Hook should have set HookAccessedContext = true");
        }

        [Then("timeout hook executed <{word}>")]
        public void TimeoutHookExecuted(string testId)
        {
            var executed = sc.TryGetValue("TimeoutHookExecuted", out object? value) && (bool)value;
            executed.ShouldBeTrue("Timeout-filtered hook should have executed");
        }

        [Then("verify no retry context exists")]
        public void VerifyNoRetryContextExists()
        {
            var exists = sc.TryGetRetryContext(out _);
            exists.ShouldBeFalse("Scenario should have no active retry context");
        }

        [When("check thread local is clean")]
        public void CheckThreadLocalIsClean()
        {
            var ctx = Manager.GetRetryContext(runner);
            ctx.ShouldBeNull("No retry context should be active in runner registry");
        }

        [When("check scenario context is clean")]
        public void CheckScenarioContextIsClean()
        {
            var ctx = sc.GetRetryContext();
            ctx.ShouldBeNull("ScenarioContext should have no active retry context");
        }

        [Then("verify no cross-thread contamination {word}")]
        public void VerifyNoCrossThreadContamination(string id)
        {
            var ctx = sc.GetRetryContext();
            ctx.ShouldNotBeNull($"Active retry context expected for parallel test {id}");
        }

        [Then("should fail on first attempt <{word}>")]
        public void ShouldFailOnFirstAttempt(string testId)
        {
            int actual = GetCount(testId, "attempts");
            actual.ShouldBe(1, "Condition mismatch should not retry");
        }

        [Then("should fail after {int} attempts <{word}>")]
        public void ShouldFailAfterAttempts(int expectedAttempts, string testId)
        {
            int actual = GetCount(testId, "attempts");
            actual.ShouldBe(expectedAttempts);
        }

        [Then("should fail with timeout <{word}>")]
        public void ShouldFailWithTimeout(string testId)
        {
            int actual = GetCount(testId, "attempts");
            actual.ShouldBeLessThan(10, "Timeout should stop retry before 10 attempts");
        }

        private sealed class CustomTestException(string message) : Exception(message) { }
    }
}
