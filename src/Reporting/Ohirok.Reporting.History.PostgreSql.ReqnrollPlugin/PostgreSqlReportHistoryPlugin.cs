using Ohirok.Reporting.History.PostgreSql.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(PostgreSqlReportHistoryPlugin))]

namespace Ohirok.Reporting.History.PostgreSql.ReqnrollPlugin
{
    public sealed class PostgreSqlReportHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<PostgreSqlHistoryConfig>(e,
                cfg => new PostgreSqlHistoryStorage(cfg.ConnectionString, cfg.AutoCreateDb));
        }
    }
}
