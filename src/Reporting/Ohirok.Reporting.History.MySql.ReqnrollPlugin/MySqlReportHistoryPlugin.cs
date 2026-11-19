using Ohirok.Reporting.History.MySql.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MySqlReportHistoryPlugin))]

namespace Ohirok.Reporting.History.MySql.ReqnrollPlugin
{
    public sealed class MySqlReportHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<MySqlHistoryConfig>(e,
                cfg => new MySqlHistoryStorage(cfg.ConnectionString, cfg.AutoCreateDb));
        }
    }
}
