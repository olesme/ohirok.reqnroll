using Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin;
using Reqnroll.Plugins;
using Reqnroll.UnitTestProvider;

[assembly: RuntimePlugin(typeof(CustomStoragePersistentCleanupPlugin))]
namespace Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin
{
    public sealed class CustomStoragePersistentCleanupPlugin : IRuntimePlugin
    {
        public void Initialize(RuntimePluginEvents e, RuntimePluginParameters p, UnitTestProviderConfiguration u)
        {
            // No storageFactory: the storage is supplied by the user later via UseStorage / PerformAllAsync(storage),
            // so autoPerformOnStart has nothing to run at this point (see README).
            PersistentCleanupBootstrap.Setup<CleanupConfigBase>(e);
        }
    }
}
