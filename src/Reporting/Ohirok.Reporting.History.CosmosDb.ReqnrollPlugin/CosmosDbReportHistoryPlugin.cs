using Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(CosmosDbReportHistoryPlugin))]

namespace Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin
{
    public sealed class CosmosDbReportHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<CosmosDbHistoryConfig>(e,
                cfg => new CosmosDbHistoryStorage(cfg.ConnectionString, cfg.DatabaseId, cfg.ContainerId));
        }
    }
}
