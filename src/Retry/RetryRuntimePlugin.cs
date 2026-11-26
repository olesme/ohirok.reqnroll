using Ohirok.CustomBindings;
using Ohirok.Retry.Config;
using Ohirok.Retry.Core;
using Ohirok.Retry.Execution;
using Ohirok.Retry.Helpers;
using Ohirok.RuntimeMiddleware;
using Reqnroll;
using Reqnroll.Bindings;
using Reqnroll.Events;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(Ohirok.Retry.ReqnrollPlugin.RetryRuntimePlugin))]

namespace Ohirok.Retry.ReqnrollPlugin
{
    public sealed class RetryRuntimePlugin : IRuntimePlugin
    {
        public void Initialize(
            RuntimePluginEvents runtimePluginEvents,
            RuntimePluginParameters runtimePluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            // Also registers the SDK's DeferredOutput flush hook, which writes retry completion signals.
            CustomBindingsBootstrap.Setup(runtimePluginEvents);

            runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
            {
                args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, RetryBindingInvoker>(
                    order: RetryConfig.BindingInvokerOrder);
            };

            runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
            {
                var eventPublisher = args.ObjectContainer.Resolve<ITestThreadExecutionEventPublisher>();

                eventPublisher.AddHandler<ScenarioStartedEvent>(async evt =>
                {
                    if (evt.ScenarioContext is not ScenarioContext sc) return;

                    var runner = sc.ScenarioContainer.Resolve<ITestRunner>();
                    var ctx = Manager.GetRetryContext(runner);
                    if (ctx == null) return;

                    ctx.CurrentScenarioContext = sc;

                    if (ctx.Attempt == 0)
                    {
                        ctx.Type = RetryType.Test;
                        ctx.InitFromScenarioContext(sc);

                        if (!ctx.AllTags.Any(t => t.StartsWith("retry", StringComparison.OrdinalIgnoreCase)))
                            return;

                        sc.ScenarioContainer.RegisterInstanceAs(ctx);
                        sc.Set(ctx);
                        RetryTagParser.ParseTags(ctx);
                        ctx.StartLoop();
                        ctx.StartAttempt();
                        await RetryExecutor.OnLoopStartedAsync(ctx, sc);
                    }
                    else
                    {
                        sc.ScenarioContainer.RegisterInstanceAs(ctx);
                        sc.Set(ctx);

                        if (ctx.ScenarioBackup != null)
                            foreach (var kvp in ctx.ScenarioBackup)
                                sc[kvp.Key] = kvp.Value;

                        ctx.StartAttempt();
                    }
                });
            };
        }
    }
}
