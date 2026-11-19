using Ohirok.Reporting.History.Sqlite.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(SqliteHistoryPlugin))]

namespace Ohirok.Reporting.History.Sqlite.ReqnrollPlugin
{
    public sealed class SqliteHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<SqliteHistoryConfig>(e,
                cfg => new SqliteHistoryStorage(cfg.DbPath, cfg.AutoCreateDb));
        }
    }
}
