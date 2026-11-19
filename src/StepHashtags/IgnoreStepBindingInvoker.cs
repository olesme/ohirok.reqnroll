using Ohirok.PluginChains.Common.Reporting;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Events;
using Reqnroll.Infrastructure;
using Reqnroll.Tracing;

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    internal sealed class IgnoreStepBindingInvoker(IAsyncBindingInvoker inner) : IAsyncBindingInvoker
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

            if (sec.ShouldSkip(out var reason))
            {
                testTracer.TraceStepSkipped(new Exception(reason));
                var publisher = sc.ScenarioContainer.Resolve<ITestThreadExecutionEventPublisher>();
                await publisher.PublishEventAsync(new StepSkippedEvent());
                foreach (var handler in sc.ScenarioContainer.ResolveAll<ISkippedStepHandler>())
                    handler.Handle(sc);
                await publisher.PublishEventAsync(new OutputAddedEvent(
                    $"-> skipped: {reason}",
                    contextManager.FeatureContext.FeatureInfo,
                    sc.ScenarioInfo));
                if (IgnoreStepConfig.MarkSkippedInReport)
                    contextManager.StepContext.Status = ScenarioExecutionStatus.Skipped;

                sc.ScenarioContainer.Resolve<IReqnrollOutputHelper>().ReportIssue(
                    ReportIssueCategory.StepSkipped, "Ohirok.StepHashtags", reason,
                    tags: sc.ScenarioInfo.Tags, hashtags: sec.Hashtags);

                return null;
            }

            return await inner.InvokeBindingAsync(binding, contextManager, arguments, testTracer, durationHolder);
        }
    }
}
