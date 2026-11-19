using Ohirok.CustomBindings;
using Ohirok.RuntimeMiddleware;
using Reqnroll.Bindings;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(Ohirok.XFail.ReqnrollPlugin.XFailRuntimePlugin))]

namespace Ohirok.XFail.ReqnrollPlugin
{
    public sealed class XFailRuntimePlugin : IRuntimePlugin
    {
        public void Initialize(
            RuntimePluginEvents runtimePluginEvents,
            RuntimePluginParameters runtimePluginParameters,
            UnitTestProviderConfiguration unitTestProviderConfiguration)
        {
            CustomBindingsBootstrap.Setup(runtimePluginEvents);

            // The plugin's own [AfterScenario] hook (InternalXFailHooks) is discovered like a step assembly's.
            runtimePluginEvents.ConfigurationDefaults += (_, args) =>
            {
                var assemblyName = typeof(XFailRuntimePlugin).Assembly.GetName().Name;
                if (!args.ReqnrollConfiguration.AdditionalStepAssemblies.Contains(assemblyName))
                    args.ReqnrollConfiguration.AdditionalStepAssemblies.Add(assemblyName);
            };

            runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
            {
                if (!XFailConfig.Enabled) return;

                args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, XFailBindingInvoker>(
                    order: XFailConfig.BindingInvokerOrder);
            };
        }
    }
}
