using Ohirok.Reporting.History.MongoDb.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MongoDbReportHistoryPlugin))]

namespace Ohirok.Reporting.History.MongoDb.ReqnrollPlugin
{
    public sealed class MongoDbReportHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<MongoDbHistoryConfig>(e,
                cfg => new MongoDbHistoryStorage(cfg.ConnectionString, cfg.DatabaseName, cfg.CollectionName));
        }
    }
}
