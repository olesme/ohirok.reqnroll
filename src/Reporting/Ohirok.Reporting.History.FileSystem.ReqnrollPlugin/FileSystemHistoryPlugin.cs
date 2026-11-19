using Ohirok.Reporting.History.FileSystem.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(FileSystemHistoryPlugin))]

namespace Ohirok.Reporting.History.FileSystem.ReqnrollPlugin
{
    public sealed class FileSystemHistoryPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            ReportHistoryBootstrap.Setup<FileSystemHistoryConfig>(e,
                cfg => new FileSystemHistoryStorage(cfg.FilePath, cfg.AutoCreateDb));
        }
    }
}
