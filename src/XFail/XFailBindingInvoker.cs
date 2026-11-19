using System.Reflection;
using Ohirok.CustomBindings;
using Ohirok.PluginChains.Common;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.StepHashtags.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Execution;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Infrastructure;
using Reqnroll.Tracing;

namespace Ohirok.XFail.ReqnrollPlugin
{
    internal sealed class XFailBindingInvoker(IAsyncBindingInvoker inner) : IAsyncBindingInvoker
    {
        public async Task<object> InvokeBindingAsync(
            IBinding binding,
            IContextManager contextManager,
            object[] arguments,
            ITestTracer testTracer,
            DurationHolder durationHolder)
        {
            if (binding is not IStepDefinitionBinding)
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);

            var sc = contextManager.ScenarioContext;
            var sec = sc.ScenarioContainer.Resolve<StepExecutionContext>();

            if (!TryGetFailExpectation(sec, out FailType mode, out string[] expectedExceptions))
                return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);

            object result;
            try
            {
                result = await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
            }
            catch (Exception ex)
            {
                var real = (ex as TargetInvocationException)?.InnerException ?? ex;

                if (MatchesByPatterns(real, expectedExceptions))
                {
                    OutputExpectedFailure(sc, mode, real);
                    await DispatchHookAsync(sc, mode, real, sec.Hashtags);
                    return null;
                }

                var expectedStr = expectedExceptions != null && expectedExceptions.Length > 0
                    ? string.Join("|", expectedExceptions)
                    : "(any exception)";

                throw XFailOptions.ExceptionFactory.WrongException(
                    $"{(mode == FailType.XFail ? "Expected" : "Allowed")} {expectedStr}, got {real.GetType().FullName}: {real.Message}",
                    real);
            }

            // The step passed. This is outside the try block on purpose: the exceptions below must not be
            // caught by the handler above (it would treat the unexpected-pass exception as the expected failure).
            if (mode == FailType.XFail)
            {
                const string message = "Expected failure but step passed.";
                sc.ScenarioContainer.Resolve<IReqnrollOutputHelper>().ReportIssue(
                    ReportIssueCategory.XFailPassed, "Ohirok.XFail", message,
                    tags: sc.ScenarioInfo.Tags, hashtags: sec.Hashtags);
                throw XFailOptions.ExceptionFactory.UnexpectedPass(message);
            }

            if (XFailConfig.OutputMode != ExpectedFailureOutput.None)
            {
                sc.ScenarioContainer.Resolve<IReqnrollOutputHelper>()
                    .WriteLine("Allowed failure but step passed.");
            }

            await DispatchHookAsync(sc, mode, null, sec.Hashtags);
            return result;
        }

        private static async Task DispatchHookAsync(ScenarioContext sc, FailType mode, Exception ex, string[] hashtags)
        {
            var xfailMode = mode == FailType.XFail ? XFailMode.XFail : XFailMode.AFail;
            var ctx = new XFailScenarioContext(xfailMode, ex);
            var eventKey = mode == FailType.XFail
                ? XFailConstants.ScenarioXFailedEventKey
                : XFailConstants.ScenarioAFailedEventKey;

            var provider = sc.ScenarioContainer.Resolve<ICustomBindingProvider>();
            await XFailHooksExecutor.ExecuteAsync(provider, ctx, eventKey, sc.ScenarioContainer, hashtags);
        }

        private static void OutputExpectedFailure(ScenarioContext sc, FailType mode, Exception ex)
        {
            var outputMode = XFailConfig.OutputMode;
            if (outputMode == ExpectedFailureOutput.None) return;

            var output = sc.ScenarioContainer.Resolve<IReqnrollOutputHelper>();
            var prefix = mode == FailType.XFail ? "Expected" : "Allowed";

            if (outputMode == ExpectedFailureOutput.Brief)
            {
                var exName = ex.GetType().Name;
                output.WriteLine($"{prefix} failure hit: {exName}: {ex.Message}");
            }
            else if (outputMode == ExpectedFailureOutput.Full)
            {
                output.WriteLine($"{prefix} failure hit:");
                output.WriteLine(ex.ToString());
            }
        }

        private enum FailType
        {
            None = 0,
            XFail = 1,
            AFail = 2
        }

        private static readonly string[] XFailKeywords = XFailTags.XFail;
        private static readonly string[] AFailKeywords = XFailTags.AFail;

        private static bool TryGetFailExpectation(StepExecutionContext sec, out FailType type, out string[] args)
        {
            type = FailType.None;

            var hashtags = sec.Hashtags;
            if (hashtags.Length == 0)
            {
                args = [];
                return false;
            }

            foreach (var h in hashtags)
            {
                if (TagValueParser.TryExtractValues(h, XFailKeywords, out args))
                {
                    type = FailType.XFail;
                    return true;
                }
            }
            foreach (var h in hashtags)
            {
                if (TagValueParser.TryExtractValues(h, AFailKeywords, out args))
                {
                    type = FailType.AFail;
                    return true;
                }
            }
            args = [];
            return false;
        }

        private static bool MatchesByPatterns(Exception ex, string[] patterns)
        {
            if (patterns == null || patterns.Length == 0) return true; // any exception matches

            var name = ex.GetType().Name;
            var full = ex.GetType().FullName ?? "";

            return TagValueParser.MatchesPatternRules([name, full], patterns);
        }
    }
}
