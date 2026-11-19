using Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(CosmosDbPersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin
{
    public sealed class CosmosDbPersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<CosmosDbCleanupConfig>(e,
                cfg => new CosmosDbCleanupStorage(cfg.ConnectionString, cfg.DatabaseId, cfg.ContainerId));
        }
    }
}
