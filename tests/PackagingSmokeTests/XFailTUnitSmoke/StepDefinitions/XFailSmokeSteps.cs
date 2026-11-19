using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;
using Reqnroll;

namespace XFailSmoke.StepDefinitions
{
    [Binding]
    public sealed class XFailSmokeSteps
    {
        private static int s_xfailHookCount;

        [BeforeScenario]
        public static void ResetHookCount() => s_xfailHookCount = 0;

        [Given("the system is ready")]
        public void SystemIsReady()
        {
        }

        [When("the known-bug step runs")]
        public void KnownBugStepRuns()
        {
            throw new InvalidOperationException("Intentional xfail trigger");
        }

        [When("the flaky step times out")]
        public void FlakyStepTimesOut()
        {
            throw new TimeoutException("Intentional afail trigger");
        }

        [Then("the xfail hook observed a failure")]
        public void XFailHookObservedAFailure()
        {
            // Reaching this step at all already proves the preceding step's exception was intercepted
            // rather than propagated; the hook count proves XFail actually matched the hashtag instead
            // of some unrelated layer swallowing it.
            if (s_xfailHookCount != 1)
                throw new Exception($"Expected the OnXFail hook to fire once, fired {s_xfailHookCount} times");
        }

        [Then("the scenario reached its final step")]
        public void ScenarioReachedItsFinalStep()
        {
        }

        [OnXFail]
        public void OnXFail(XFailScenarioContext ctx)
        {
            if (!ctx.Failed || ctx.Exception is not InvalidOperationException)
                throw new Exception($"Unexpected xfail context: Failed={ctx.Failed}, Exception={ctx.Exception}");
            s_xfailHookCount++;
        }
    }
}
