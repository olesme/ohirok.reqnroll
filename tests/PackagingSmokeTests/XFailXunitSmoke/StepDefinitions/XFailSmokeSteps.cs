using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;
using Reqnroll;

namespace XFailSmoke.StepDefinitions
{
    [Binding]
    public sealed class XFailSmokeSteps(ScenarioContext scenarioContext)
    {
        // Counted per scenario: the runners execute scenarios in parallel, and the @xfail scenario fires OnXFail too.
        private const string HookCountKey = "xfailHookCount";

        private int HookCount => scenarioContext.TryGetValue(HookCountKey, out int count) ? count : 0;

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
            if (HookCount != 1)
                throw new Exception($"Expected the OnXFail hook to fire once, fired {HookCount} times");
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
            scenarioContext[HookCountKey] = HookCount + 1;
        }
    }
}
