using Ohirok.SoftAssert.Attributes;
using Ohirok.SoftAssert.Core;
using Ohirok.StepHashtags.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;
using Reqnroll;

namespace AllPluginsSmoke.StepDefinitions
{
    [Binding]
    public sealed class AllPluginsSmokeSteps(ScenarioContext sc)
    {
        // Intentionally never reset in a [BeforeScenario] hook. Retry re-invokes the whole scenario,
        // hooks included, so a reset there would wipe the attempt counter on every retry and the
        // retry scenario could never converge. Assertions below are written to tolerate the resulting
        // cross-scenario accumulation instead.
        private static int s_attempt;
        private static int s_xfailHookCount;
        private static int s_softFailureCount;

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

        [When("I call the external service")]
        public void CallService()
        {
            var tags = sc.GetStepHashtags();
            if (!sc.HasStepHashtag("timeout:5000"))
                throw new Exception($"Expected timeout:5000 hashtag, got [{string.Join(",", tags)}]");
            if (!sc.HasStepHashtag("priority:high"))
                throw new Exception($"Expected priority:high hashtag, got [{string.Join(",", tags)}]");
        }

        [When("the known-bug step runs")]
        public void KnownBugStepRuns()
        {
            throw new InvalidOperationException("Intentional xfail trigger");
        }

        [Then("the xfail hook observed a failure")]
        public void XFailHookObservedAFailure()
        {
            if (s_xfailHookCount < 1)
                throw new Exception("Expected the OnXFail hook to fire at least once, it never fired");
        }

        [Then("the soft step is fine")]
        public void SoftStepIsFine()
        {
        }

        [Then("the browser variant is set")]
        public void BrowserVariantIsSet()
        {
            var browser = sc.ScenarioInfo.Arguments["Browser"] as string;
            if (browser is not ("Chrome" or "Firefox"))
                throw new Exception($"Expected the Browser variant to be Chrome or Firefox, got '{browser}'");
        }

        [Then("no soft failures were captured")]
        public void NoSoftFailuresWereCaptured()
        {
            if (s_softFailureCount != 0)
                throw new Exception($"Expected zero captured soft failures, got {s_softFailureCount}");
        }

        [OnXFail]
        public void OnXFail(XFailScenarioContext ctx)
        {
            if (!ctx.Failed || ctx.Exception is not InvalidOperationException)
                throw new Exception($"Unexpected xfail context: Failed={ctx.Failed}, Exception={ctx.Exception}");
            s_xfailHookCount++;
        }

        [OnSoftFailure]
        public void OnSoftFailure(SoftAssertContext ctx)
        {
            s_softFailureCount++;
        }
    }
}
