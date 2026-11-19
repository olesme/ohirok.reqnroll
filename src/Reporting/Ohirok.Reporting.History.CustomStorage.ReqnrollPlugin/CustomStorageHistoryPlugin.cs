using Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(CustomStorageHistoryPlugin))]

namespace Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin
{
    public sealed class CustomStorageHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<ReportHistoryConfigBase>(e);
        }
    }
}
