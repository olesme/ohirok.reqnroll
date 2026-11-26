using Ohirok.CustomBindings;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Execution;
using Reqnroll;

namespace Ohirok.XFail
{
    /// <summary>Called by the code the XFail generator plugin adds to a scenario tagged <c>@xfail</c> / <c>@afail</c>.</summary>
    public static class Manager
    {
        /// <summary>
        /// Runs the generated test method body and judges its outcome against the scenario's tag: a failure the tag
        /// expects or allows passes the test, an unexpected pass of <c>@xfail</c> fails it, any other exception fails it
        /// as a wrong exception. Covers everything in the test method - Before hooks, steps and steps or calls other
        /// plugins inject - but not <c>AfterScenario</c> hooks, which Reqnroll runs after the test method.
        /// </summary>
        public static async Task WrapScenarioAsync(ITestRunner testRunner, Func<Task> scenarioBody)
        {
            Exception? captured = null;
            try
            {
                await scenarioBody();
            }
            catch (Exception ex)
            {
                captured = ex;
            }

            var sc = testRunner.ScenarioContext;
            var tags = sc.ScenarioInfo.CombinedTags ?? sc.ScenarioInfo.Tags;
            var mode = XFailMode.XFail;
            string[] patterns = [];
            if (!tags.Any(t => XFailTags.TryParse(t, out mode, out patterns)))
            {
                if (captured != null) throw captured;
                return;
            }

            var verb = mode == XFailMode.XFail ? "Expected" : "Allowed";
            var expected = patterns.Length > 0 ? string.Join("|", patterns) : "a failure";

            if (captured != null && !XFailTags.Matches(captured, patterns))
            {
                var wrong = $"{verb} {expected}, got {captured.GetType().FullName}: {captured.Message}";
                DeferredOutput.Enqueue(sc, output => output.WriteLine($"[XFail] {wrong}"));
                throw XFailOptions.ExceptionFactory.WrongException(wrong, captured);
            }

            var provider = sc.ScenarioContainer.Resolve<ICustomBindingProvider>();
            await XFailHooksExecutor.ExecuteAsync(provider, new XFailScenarioContext(mode, captured),
                mode == XFailMode.XFail ? XFailConstants.ScenarioXFailedEventKey : XFailConstants.ScenarioAFailedEventKey,
                sc.ScenarioContainer, tags);

            if (captured != null)
            {
                // The test passes; the report shows the scenario as passed with an issue rather than as failed.
                var message = $"{verb} failure: {captured.GetType().Name}: {captured.Message}";
                DeferredOutput.Enqueue(sc, output => output.ReportIssue(
                    ReportIssueCategory.ExpectedFailure, "Ohirok.XFail", message, tags: tags));
                return;
            }

            if (mode == XFailMode.XFail)
            {
                const string message = "Expected failure but scenario passed.";
                DeferredOutput.Enqueue(sc, output => output.ReportIssue(
                    ReportIssueCategory.XFailPassed, "Ohirok.XFail", message, tags: tags));
                throw XFailOptions.ExceptionFactory.UnexpectedPass(message);
            }
        }
    }
}
