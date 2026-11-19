using Ohirok.StepHashtags.ReqnrollPlugin;
using Reqnroll;

namespace Ohirok.StepHashtags
{
    public static class Manager
    {
        /// <summary>
        /// Sets step hashtags for the current step in the test runner.
        /// Should be called only from generated test classes.
        /// </summary>
        public static void Set(ITestRunner runner, string[] hashtags)
        {
            runner.ScenarioContext.ScenarioContainer.Resolve<StepExecutionContext>().Set(hashtags);
        }

        /// <summary>
        /// Clears step hashtags for the current step in the test runner.
        /// Should be called only from generated test classes.
        /// </summary>
        public static void Reset(ITestRunner runner)
        {
            runner.ScenarioContext.ScenarioContainer.Resolve<StepExecutionContext>().Reset();
        }
    }
}
