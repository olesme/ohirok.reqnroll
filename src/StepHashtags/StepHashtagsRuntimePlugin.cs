using Ohirok.PluginChains.Common;
using Ohirok.RuntimeMiddleware;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Events;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(Ohirok.StepHashtags.ReqnrollPlugin.StepHashtagsRuntimePlugin))]

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    public sealed class StepHashtagsRuntimePlugin : IRuntimePlugin
    {
        public void Initialize(
            RuntimePluginEvents runtimePluginEvents,
            RuntimePluginParameters runtimePluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            PluginConfigProvider.Initialize(runtimePluginEvents);

            runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
            {
                // Test-thread level: the chain wraps whatever IAsyncBindingInvoker the global container holds.
                if (StepHashtagsConfig.Enabled && IgnoreStepConfig.Enabled)
                {
                    args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, IgnoreStepBindingInvoker>(
                        order: IgnoreStepConfig.BindingInvokerOrder);
                }

                // Report output needs no plumbing here: per-step hashtags are fully static (derivable from the
                // .feature file's own comments), so the report formatter derives them itself via
                // StaticHashtagAssociator instead of round-tripping them through runtime output. Only the
                // StdOut echo - a genuinely runtime, console-visible convenience - remains here.
                var wantsStdOut = StepHashtagsConfig.Output == HashtagsOutput.StdOut;
                if (!wantsStdOut) return;

                // Writing must happen from here (a StepStartedEvent handler), not from StepExecutionContext.Set() -
                // Set() runs before testRunner.Given/When/Then(...) is called, i.e. before Reqnroll's formatter
                // pipeline has pushed a step tracker for this step; any output written that early is silently lost.
                var eventPublisher = args.ObjectContainer.Resolve<ITestThreadExecutionEventPublisher>();
                eventPublisher.AddHandler<StepStartedEvent>(async evt =>
                {
                    var hashtags = ((ScenarioContext)evt.ScenarioContext).GetStepHashtags();
                    if (!hashtags.Any()) return;

                    var hashtagsText = string.Join(" ", hashtags.Select(static h => $"#{h}"));
                    await eventPublisher.PublishEventAsync(new OutputAddedEvent(
                        hashtagsText,
                        evt.FeatureContext.FeatureInfo,
                        evt.ScenarioContext.ScenarioInfo));
                });
            };
            runtimePluginEvents.CustomizeScenarioDependencies += (_, args) =>
            {
                if (!StepHashtagsConfig.Enabled) return;
                args.ObjectContainer.RegisterFactoryAs<StepExecutionContext>(
                    c => new StepExecutionContext(c.Resolve<ScenarioContext>())
                );
            };
        }
    }
}
