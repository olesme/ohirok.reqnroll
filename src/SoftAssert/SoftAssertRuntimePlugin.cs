using Ohirok.CustomBindings;
using Ohirok.RuntimeMiddleware;
using Ohirok.SoftAssert.Config;
using Ohirok.SoftAssert.Core;
using Ohirok.SoftAssert.Execution;
using Reqnroll.Bindings;
using Reqnroll.Bindings.Reflection;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(Ohirok.SoftAssert.ReqnrollPlugin.SoftAssertRuntimePlugin))]

namespace Ohirok.SoftAssert.ReqnrollPlugin
{
    public sealed class SoftAssertRuntimePlugin : IRuntimePlugin
    {
        public void Initialize(
            RuntimePluginEvents runtimePluginEvents,
            RuntimePluginParameters runtimePluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            CustomBindingsBootstrap.Setup(runtimePluginEvents);

            // Lets Reqnroll find the generated end-of-scenario flush step (SoftFlushStep) in this assembly.
            runtimePluginEvents.ConfigurationDefaults += (_, args) =>
            {
                var assemblyName = typeof(SoftAssertRuntimePlugin).Assembly.GetName().Name;
                if (!args.ReqnrollConfiguration.AdditionalStepAssemblies.Contains(assemblyName))
                    args.ReqnrollConfiguration.AdditionalStepAssemblies.Add(assemblyName);
            };

            // Test-thread level: the chain wraps whatever IAsyncBindingInvoker the global container holds.
            runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
                args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, SoftAssertBindingInvoker>(
                    order: SoftAssertConfig.BindingInvokerOrder);

            runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
            {
                var container = args.ObjectContainer;
                var registry = container.Resolve<IBindingRegistry>();
                var factory = container.Resolve<IBindingFactory>();
                RegisterWireOnFlushHook(registry, factory);
            };
        }

        private static void RegisterWireOnFlushHook(IBindingRegistry registry, IBindingFactory factory)
        {
            var type = typeof(SoftAssertHooks);
            registry.RegisterHookBinding(factory.CreateHookBinding(
                new RuntimeBindingMethod(type.GetMethod(nameof(SoftAssertHooks.WireOnFlush))),
                HookType.BeforeScenario, null, int.MinValue));
        }
    }
}
