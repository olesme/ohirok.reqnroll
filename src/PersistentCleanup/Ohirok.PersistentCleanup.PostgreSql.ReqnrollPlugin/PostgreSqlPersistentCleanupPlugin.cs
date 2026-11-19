using Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(PostgreSqlPersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin
{
    public sealed class PostgreSqlPersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<PostgreSqlCleanupConfig>(e,
                cfg => new PostgreSqlCleanupStorage(cfg.ConnectionString, cfg.AutoCreateDb));
        }
    }
}
