using Catchy;
using Ohirok.SoftAssert.Core;
using Reqnroll;

namespace Ohirok.SoftAssert
{
    public static class Manager
    {
        public static void SetSoftStepMarker(ITestRunner testRunner, string[] hashtags)
        {
            var sc = testRunner.ScenarioContext;
            if (sc.TryGetValue<SoftAssertContext>(out _)) return;

            var ctx = SoftAssertContext.ForScenario(sc, hashtags);

            sc.ScenarioContainer.RegisterInstanceAs(ctx);
            sc.Set(ctx);
        }

        /// <summary>
        /// Generated after the last step. Does nothing (and shows nothing in the report) unless there is
        /// something to flush; otherwise runs a real step that throws the aggregate inside the test body.
        /// </summary>
        public static async Task FlushIfNeededAsync(ITestRunner testRunner)
        {
            var sc = testRunner.ScenarioContext;

            // After a failure/skip Reqnroll would only skip this step; Catchy's teardown hooks keep the old contract.
            if (sc.ScenarioExecutionStatus != ScenarioExecutionStatus.OK) return;
            if (!sc.ScenarioContainer.IsRegistered<StatefulAsserter>()) return;

            var soft = sc.ScenarioContainer.Resolve<StatefulAsserter>().Soft.SoftState;
            if (!soft.HasFailures || soft.AlreadyFlushed) return;

            await testRunner.AndAsync(SoftAssertConstants.FlushStepText, null, null);
        }
    }
}
