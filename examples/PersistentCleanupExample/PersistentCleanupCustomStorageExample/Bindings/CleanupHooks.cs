using Ohirok.PersistentCleanup;
using Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin;
using PersistentCleanupCustomStorageExample.Support;

namespace PersistentCleanupCustomStorageExample.Bindings
{
    [Binding]
    public static class CleanupHooks
    {
        public static InMemoryCleanupStorage SharedStorage { get; private set; } = null!;

        [BeforeTestRun]
        public static void WireUpCustomStorage(ICleanupService cleanupService)
        {
            SharedStorage = new InMemoryCleanupStorage();
            cleanupService.UseStorage(SharedStorage);
        }
    }
}
