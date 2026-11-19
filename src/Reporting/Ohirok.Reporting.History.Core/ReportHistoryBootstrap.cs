using Ohirok.PluginChains.Common;
using Reqnroll.Plugins;

namespace Ohirok.Reporting.History
{
    public static class ReportHistoryBootstrap
    {
        public static void Setup<TConfig>(
            RuntimePluginEvents runtimePluginEvents,
            Func<TConfig, IReportHistoryStorage> storageFactory = null)
            where TConfig : ReportHistoryConfigBase, new()
        {
            PluginConfigProvider.Initialize(runtimePluginEvents);

            runtimePluginEvents.CustomizeGlobalDependencies += async (sender, args) =>
            {
                TConfig config;
                try { config = ReportHistoryConfigHelper.GetConfig<TConfig>(); }
                catch { config = new TConfig(); }

                if (!config.Enabled) return;

                var storage = storageFactory?.Invoke(config);
                if (storage != null)
                    await storage.InitializeAsync();

                args.ObjectContainer.RegisterInstanceAs<IReportHistoryStorage>(new ReportHistoryStorageProxy(storage));
            };
        }
    }
}
