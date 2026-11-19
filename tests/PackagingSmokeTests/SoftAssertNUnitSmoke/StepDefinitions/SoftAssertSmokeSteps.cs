using Ohirok.SoftAssert.Attributes;
using Ohirok.SoftAssert.Core;
using Reqnroll;

namespace SoftAssertSmoke.StepDefinitions
{
    [Binding]
    public sealed class SoftAssertSmokeSteps
    {
        private static int s_flushCount;
        private static int s_failureCount;

        [Given("the page has loaded")]
        public void PageLoaded()
        {
        }

        [Then("the header is correct")]
        public void HeaderIsCorrect()
        {
        }

        [Then("the page is still usable")]
        public void PageStillUsable()
        {
        }

        [Then("the flush hook recorded zero failures")]
        public void FlushHookRecordedZeroFailures()
        {
            // Catchy's SoftState.FlushIfNeeded (SoftState.cs:88) returns before invoking any OnFlush
            // notifier when HasFailures is false - zero captured soft failures means the hook never
            // fires at all, not that it fires with an empty aggregate.
            if (s_flushCount != 0)
                throw new Exception($"Expected OnSoftFlush not to fire with zero failures, fired {s_flushCount} times");
            if (s_failureCount != 0)
                throw new Exception($"Expected zero captured soft failures, got {s_failureCount}");
        }

        [OnSoftFailure]
        public void OnFailure(SoftAssertContext ctx)
        {
            s_failureCount++;
        }

        [OnSoftFlush]
        public void OnFlush(SoftAssertContext ctx)
        {
            s_flushCount++;
        }
    }
}
