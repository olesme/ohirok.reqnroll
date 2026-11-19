using Ohirok.PluginChains.Common;
using Ohirok.RuntimeMiddleware;
using Reqnroll.Bindings.Discovery;
using Reqnroll.Plugins;

namespace Ohirok.CustomBindings
{
    public static class CustomBindingsBootstrap
    {
        public static void Setup(RuntimePluginEvents runtimePluginEvents)
        {
            PluginConfigProvider.Initialize(runtimePluginEvents);

            runtimePluginEvents.CustomizeGlobalDependencies += (_, args) =>
            {
                var container = args.ObjectContainer;

                if (!container.IsRegistered<CustomBindingRegistry>())
                    container.RegisterInstanceAs(new CustomBindingRegistry());

                if (!container.IsRegistered<ICustomBindingProvider>())
                {
                    var registry = container.Resolve<CustomBindingRegistry>();
                    container.RegisterInstanceAs<ICustomBindingProvider>(new CustomBindingProvider(registry));
                }

                container.RegisterChainLinkIfNotExists<IRuntimeBindingSourceProcessor,
                    RuntimeBindingSourceProcessor,
                    CustomBindingSourceProcessor>(order: 0);
            };
        }
    }
}
