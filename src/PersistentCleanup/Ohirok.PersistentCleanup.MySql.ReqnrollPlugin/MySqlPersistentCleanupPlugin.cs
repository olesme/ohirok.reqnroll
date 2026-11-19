using Ohirok.PersistentCleanup.MySql.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MySqlPersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.MySql.ReqnrollPlugin
{
    public sealed class MySqlPersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<MySqlCleanupConfig>(e,
                cfg => new MySqlCleanupStorage(cfg.ConnectionString, cfg.AutoCreateDb));
        }
    }
}
