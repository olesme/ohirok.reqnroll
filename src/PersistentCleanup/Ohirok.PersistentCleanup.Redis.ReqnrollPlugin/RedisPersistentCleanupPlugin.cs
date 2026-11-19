using Ohirok.PersistentCleanup.Redis.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(RedisPersistentCleanupPlugin))]

namespace Ohirok.PersistentCleanup.Redis.ReqnrollPlugin
{
    public sealed class RedisPersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            PersistentCleanupBootstrap.Setup<RedisCleanupConfig>(e,
                cfg => new RedisCleanupStorage(cfg.ConnectionString, cfg.KeyPrefix, cfg.Database));
        }
    }
}
