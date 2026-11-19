using Catchy;

namespace SoftAssertTests.StepDefinitions
{
    [Binding]
    public sealed class SoftAssertTestSteps(ScenarioContext sc, StatefulAsserter asserter)
    {
        [When(@"the step throws (\w+)")]
        public void TheStepThrows(string exceptionType)
        {
            Exception ex = exceptionType switch
            {
                "TimeoutException" => new TimeoutException("Simulated timeout"),
                _ => new InvalidOperationException($"Simulated failure ({exceptionType})"),
            };
            throw ex;
        }

        [Then("the next step still executes")]
        public void TheNextStepStillExecutes() => sc["NextStepExecuted"] = true;

        [Then("the next step should have executed")]
        public void VerifyNextStepExecuted()
        {
            var executed = sc.ContainsKey("NextStepExecuted") && (bool)sc["NextStepExecuted"];
            executed.ShouldBeTrue("Expected a later step to run after the #softassert step failed");
        }

        [Then("the soft state should have failures")]
        public void VerifySoftStateHasFailures() => asserter.Soft.HasFailures.ShouldBeTrue();

        [Then(@"(\w+) hook fires (\d+) time\(s\)")]
        public void HookFiresNTimes(string hookKey, int expected)
        {
            var actual = sc.ContainsKey(hookKey) ? (int)sc[hookKey] : 0;
            actual.ShouldBe(expected, $"Hook '{hookKey}' fire count mismatch");
        }

        [When("I flush the soft failures")]
        public Task Flush() => asserter.Soft.SoftState.FlushIfNeeded();
    }
}
