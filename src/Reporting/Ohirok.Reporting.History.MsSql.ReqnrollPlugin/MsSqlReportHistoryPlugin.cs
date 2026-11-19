using Ohirok.Reporting.History.MsSql.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MsSqlReportHistoryPlugin))]

namespace Ohirok.Reporting.History.MsSql.ReqnrollPlugin
{
    public sealed class MsSqlReportHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<MsSqlHistoryConfig>(e,
                cfg => new MsSqlHistoryStorage(cfg.ConnectionString, cfg.AutoCreateDb));
        }
    }
}
