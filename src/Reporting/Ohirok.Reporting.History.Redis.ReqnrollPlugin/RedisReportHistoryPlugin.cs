using Ohirok.Reporting.History.Redis.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(RedisReportHistoryPlugin))]

namespace Ohirok.Reporting.History.Redis.ReqnrollPlugin
{
    public sealed class RedisReportHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<RedisHistoryConfig>(e,
                cfg => new RedisHistoryStorage(cfg.ConnectionString, cfg.KeyPrefix, cfg.Database));
        }
    }
}
