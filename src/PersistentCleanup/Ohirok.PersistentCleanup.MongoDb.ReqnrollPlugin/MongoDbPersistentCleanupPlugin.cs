using Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(MongoDbPersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin
{
    public sealed class MongoDbPersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<MongoDbCleanupConfig>(e,
                cfg => new MongoDbCleanupStorage(cfg.ConnectionString, cfg.DatabaseName, cfg.CollectionName));
        }
    }
}
