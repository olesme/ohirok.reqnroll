using Ohirok.Retry;
using Reqnroll;

namespace RetrySmoke.StepDefinitions
{
    [Binding]
    public sealed class RetrySmokeSteps
    {
        private static int s_attempt;

        [When("operation fails {int} times")]
        public void OperationFails(int times)
        {
            s_attempt++;
            if (s_attempt <= times)
                throw new InvalidOperationException($"Intentional failure on attempt {s_attempt}");
        }

        [Then("verify attempt count is {int}")]
        public void VerifyAttemptCount(int expected)
        {
            if (s_attempt != expected)
                throw new Exception($"Expected attempt count {expected}, got {s_attempt}");
        }
    }
}
