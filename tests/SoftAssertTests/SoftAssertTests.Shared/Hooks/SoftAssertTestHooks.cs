using Catchy;
using Ohirok.SoftAssert.Attributes;

namespace SoftAssertTests.Hooks
{
    [Binding]
    public sealed class SoftAssertTestHooks(ScenarioContext sc)
    {
        private void Increment(string key) => sc[key] = sc.ContainsKey(key) ? (int)sc[key] + 1 : 1;

        [BeforeScenario("@swallow_flush")]
        public void SwallowFlush(StatefulAsserter asserter)
        {
            var expectTagFiltered = sc.ScenarioInfo.Tags.Any(t => t.TrimStart('@').Equals("tag_filtered", StringComparison.OrdinalIgnoreCase));

            // Verified here (rather than via a Gherkin `Then` step) because flush only happens
            // during AfterScenario/TearDown, after the scenario's last step has already run.
            // Throwing from FlushAction (unlike OnFlush notifiers, which swallow exceptions)
            // still fails the NUnit test if the expectation is wrong.
            asserter.Soft.SoftState.FlushAction = _ =>
            {
                var flushFired = sc.TryGetValue("soft_flush_fired", out object? value) ? (int)value : 0;
                flushFired.ShouldBe(1, "Expected [OnSoftFlush] to fire exactly once during flush");

                var tagFilteredFired = sc.TryGetValue("soft_flush_tag_filtered_fired", out object? value1) ? (int)value1 : 0;
                tagFilteredFired.ShouldBe(expectTagFiltered ? 1 : 0, "Expected [OnSoftFlush(\"@tag_filtered\")] fire count to match the scenario's tags");

                return Task.CompletedTask;
            };
        }

        [OnSoftFailure]
        public void OnAnyFailure() => Increment("soft_failure_fired");

        [OnSoftFlush]
        public void OnAnyFlush() => Increment("soft_flush_fired");

        [OnSoftFailure("@tag_filtered")]
        public void OnTagFilteredFailure() => Increment("soft_failure_tag_filtered_fired");

        [OnSoftFlush("@tag_filtered")]
        public void OnTagFilteredFlush() => Increment("soft_flush_tag_filtered_fired");

        [OnSoftFailure("ex:TimeoutException")]
        public void OnTimeoutFailure() => Increment("soft_failure_timeout_fired");
    }
}
