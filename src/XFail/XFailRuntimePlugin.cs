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
            // Also registers the SDK's DeferredOutput flush hook, which writes the scenario verdict's report signal.
            CustomBindingsBootstrap.Setup(runtimePluginEvents);

            runtimePluginEvents.CustomizeTestThreadDependencies += (_, args) =>
            {
                if (!XFailConfig.Enabled) return;

                args.ObjectContainer.RegisterChainLink<IAsyncBindingInvoker, BindingInvoker, XFailBindingInvoker>(
                    order: XFailConfig.BindingInvokerOrder);
            };
        }
    }
}
