using Ohirok.PluginChains.Common;

namespace Ohirok.PersistentCleanup.CustomStorage.ReqnrollPlugin
{
    public static class CleanupManagerExtensions
    {
        /// <summary>
        /// Set the custom storage implementation. The storage is initialized lazily on first use,
        /// and records registered before this call are flushed into it.
        /// Throws if a storage is already set and ignoreIfExists = false (default);
        /// keeps the existing storage silently if ignoreIfExists = true.
        /// </summary>
        public static void UseStorage(this ICleanupService manager, ICleanupStorage storage, bool ignoreIfExists = false)
        {
            ((ICleanupStorageManager)manager).UseStorage(
                storage, ignoreIfExists ? StorageReplaceMode.IgnoreIfExists : StorageReplaceMode.ThrowIfExists);
        }

        /// <summary>
        /// Set the custom storage (ignored if one is already set) and perform all leftover cleanups.
        /// This is the one-liner for a [BeforeTestRun] hook.
        /// </summary>
        public static async Task<CleanupExecutionResult> PerformAllAsync(
            this ICleanupService manager, ICleanupStorage storage, CancellationToken ct = default)
        {
            ((ICleanupStorageManager)manager).UseStorage(storage, StorageReplaceMode.IgnoreIfExists);
            return await manager.PerformAllAsync(ct);
        }
    }
}
