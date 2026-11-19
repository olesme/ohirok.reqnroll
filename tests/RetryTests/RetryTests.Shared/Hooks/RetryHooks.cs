using Ohirok.Retry.Attributes;
using Ohirok.Retry.Core;
using Ohirok.Retry.Extensions;

namespace RetryTests.Hooks
{
    [Binding]
    public sealed class RetryHooks(ScenarioContext sc, IReqnrollOutputHelper output)
    {
        private void Increment(string key) =>
            sc[key] = sc.TryGetValue(key, out object? value) ? (int)value + 1 : 1;

        [BeforeRetryLoop(RetryType.Test)]
        public void TestLoopStarted() => Increment("test_loop_started");

        [OnRetry(RetryType.Test)]
        public void TestRestarted(RetryContext context)
        {
            Increment("test_restarted");
            sc["FinalAttemptCount"] = context.Attempt;
            sc["HookAccessedContext"] = true;
        }

        [AfterRetryLoop(RetryType.Test)]
        public void TestLoopEnded() => Increment("test_loop_ended");

        [BeforeRetryLoop(RetryType.Block)]
        public void BlockLoopStarted() => Increment("block_loop_started");

        [OnRetry(RetryType.Block)]
        public void BlockRestarted() => Increment("block_restarted");

        [AfterRetryLoop(RetryType.Block)]
        public void BlockLoopEnded() => Increment("block_loop_ended");

        [BeforeRetryLoop(RetryType.Step)]
        public void StepLoopStarted() => Increment("step_loop_started");

        [OnRetry(RetryType.Step)]
        public void StepRestarted() => Increment("step_restarted");

        [AfterRetryLoop(RetryType.Step)]
        public void StepLoopEnded() => Increment("step_loop_ended");

        [OnRetry(RetryType.Test, "ex:TimeoutException")]
        public void TimeoutHookFired() => sc["TimeoutHookExecuted"] = true;

        [OnRetry(RetryType.Test, "@backup")]
        public void BackupScenarioDic() => sc.MigrateToNextAttempt();

        [BeforeRetryLoop(RetryType.Test, "@tag_filter_test")]
        public void TagFilteredBeforeLoop() => Increment("tag_filtered_before");

        [AfterRetryLoop(RetryType.Test, "@tag_filter_test", "result:success")]
        public void TagFilteredAfterSuccess() => Increment("tag_filtered_after_success");

        [AfterRetryLoop(RetryType.Test, "@tag_filter_test", "result:failure")]
        public void TagFilteredAfterFailure() => Increment("tag_filtered_after_failure");

        [BeforeRetryLoop(RetryType.Test, "@tagscheck")]
        [BeforeRetryLoop(RetryType.Block, "@tagscheck")]
        public void TagsCheck(RetryContext context)
        {
            var ftTags = sc.ScenarioContainer.Resolve<FeatureContext>().FeatureInfo.Tags;
            output.WriteLine("Feature tags: " + string.Join(" ", ftTags));
            var scTags = sc.ScenarioInfo.Tags;
            output.WriteLine("Scenario tags: " + string.Join(" ", scTags));
            output.WriteLine("Retry all tags: " + string.Join(" ", context.AllTags));
        }

        [AfterRetryLoop(RetryType.Test, "@print_test_loop_ended")]
        public void PrintTestLoopEnded(RetryContext context) =>
            output.WriteLine($"[Retry] Test loop ended after {context.Attempt} attempt(s), status: {context.Status}");

        [AfterRetryLoop(RetryType.Block, "@print_block_loop_ended")]
        public void PrintBlockLoopEnded(RetryContext context) =>
            output.WriteLine($"[Retry] Block loop ended after {context.Attempt} attempt(s), status: {context.Status}");

        [AfterRetryLoop(RetryType.Test, "@print_hook_access_count")]
        public void PrintHookAccessCount(RetryContext context) =>
            output.WriteLine($"[Retry] Hook access count: {context.Attempt}");

        [OnRetryDecision(RetryType.Test)]
        public void TestDecisionFired() => Increment("test_decision_fired");

        [OnRetryDecision(RetryType.Test, "@force_retry_test")]
        public void ForceRetryTest(RetryContext context) => context.ForceRetry();

        [OnRetryDecision(RetryType.Test, "@force_stop_test")]
        public void ForceStopTest(RetryContext context) => context.ForceStop();

        [OnRetryDecision(RetryType.Block, "@force_retry_block")]
        public void ForceRetryBlock(RetryContext context) => context.ForceRetry();

        [OnRetryDecision(RetryType.Step, "@force_retry_step")]
        public void ForceRetryStep(RetryContext context) => context.ForceRetry();

        [OnRetryDecision(RetryType.Step, "@force_stop_step")]
        public void ForceStopStep(RetryContext context) => context.ForceStop();

        [AfterScenario("assert_clean_context", Order = int.MaxValue - 1)]
        public void AssertCleanContextAfterScenario()
        {
            var exists = sc.TryGetRetryContext(out _);
            exists.ShouldBeFalse("RetryContext should be cleaned up after scenario block retry");
        }
    }
}
