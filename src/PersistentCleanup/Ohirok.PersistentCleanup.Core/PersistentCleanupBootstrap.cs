using Ohirok.CustomBindings;
using Ohirok.PluginChains.Common;
using Reqnroll.Events;
using Reqnroll.Plugins;

namespace Ohirok.PersistentCleanup
{
    public static class PersistentCleanupBootstrap
    {
        public static void Setup<TConfig>(
            RuntimePluginEvents runtimePluginEvents,
            Func<TConfig, ICleanupStorage> storageFactory = null)
            where TConfig : CleanupConfigBase, new()
        {
            PluginConfigProvider.Initialize(runtimePluginEvents);
            CustomBindingsBootstrap.Setup(runtimePluginEvents);

            // Note: this handler is deliberately synchronous. Everything that is asynchronous
            // (storage initialization, flushing buffered records) is done lazily by CleanupService
            // on first use, so the container is never left half-configured.
            runtimePluginEvents.CustomizeGlobalDependencies += (sender, args) =>
            {
                TConfig config;
                try { config = CleanupConfigHelper.GetConfig<TConfig>(); }
                catch { config = new TConfig(); }

                var scopeOverride = Environment.GetEnvironmentVariable(CleanupConfigBase.ScopeEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(scopeOverride))
                    config.Scope = scopeOverride;

                if (!config.Enabled || !CustomBindingsConfig.AllowCustomBindings) return;

                var bindingProvider = args.ObjectContainer.Resolve<ICustomBindingProvider>();
                ICleanupStorage storage = storageFactory?.Invoke(config);

                var manager = new CleanupService(bindingProvider, args.ObjectContainer, storage, config);
                args.ObjectContainer.RegisterInstanceAs<ICleanupService>(manager);

                // Only storages created by an adapter plugin exist at this point.
                // A custom storage is attached later (UseStorage), so it performs leftovers from its own
                // [BeforeTestRun] hook instead - see the CustomStorage README.
                if (storage != null && config.AutoPerformOnStart)
                {
                    var eventPublisher = args.ObjectContainer.Resolve<ITestThreadExecutionEventPublisher>();
                    eventPublisher.AddHandler<TestRunStartedEvent>(async evt => await manager.PerformAllAsync());
                }
            };
        }
    }
}
