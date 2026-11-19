using Ohirok.PluginChains.Common;

namespace Ohirok.Reporting.History.CustomStorage.ReqnrollPlugin
{
    public static class ReportHistoryManagerExtensions
    {
        /// <summary>
        /// Set the custom storage implementation.
        /// Throws if a storage is already configured, unless ignoreIfExists = true.
        /// </summary>
        public static void UseStorage(this IReportHistoryStorage manager, IReportHistoryStorage storage, bool ignoreIfExists = false)
        {
            ((IReportHistoryStorageManager)manager).UseStorage(storage, ignoreIfExists ? StorageReplaceMode.IgnoreIfExists : StorageReplaceMode.ThrowIfExists);
        }
    }
}
