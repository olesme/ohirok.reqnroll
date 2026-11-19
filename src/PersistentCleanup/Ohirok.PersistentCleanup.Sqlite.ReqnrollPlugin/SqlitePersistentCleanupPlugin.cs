using Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(SqlitePersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin
{
    public sealed class SqlitePersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<SqliteCleanupConfig>(e,
                cfg => new SqliteCleanupStorage(cfg.DbPath, cfg.AutoCreateDb));
        }
    }
}
