using Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MsSqlPersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin
{
    public sealed class MsSqlPersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<MsSqlCleanupConfig>(e,
                cfg => new MsSqlCleanupStorage(cfg.ConnectionString, cfg.AutoCreateDb));
        }
    }
}
