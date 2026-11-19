using Catchy;
using Catchy.Sdk;
using Ohirok.Retry.Extensions;

namespace RetryTests.StepDefinitions
{
    [Binding]
    public sealed class SoftCheckSteps(ScenarioContext sc, StatefulAsserter asserter)
    {
        // Records a soft failure directly (no #softassert marker, nothing is thrown) while the attempt <= times.
        [When("a soft check fails {int} time(s) <{word}>")]
        public void SoftCheckFails(int times, string testId)
        {
            var attempt = sc.GetRetryContext()?.Attempt ?? 1;
            if (attempt <= times)
                asserter.Soft.SoftState.Capture(
                    new InvalidOperationException($"Soft failure on attempt {attempt} ({testId})"));
        }
    }
}
