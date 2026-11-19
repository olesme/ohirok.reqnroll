using StorageReplaceMode = Ohirok.PluginChains.Common.StorageReplaceMode;

namespace Ohirok.Reporting.History
{
    internal interface IReportHistoryStorageManager
    {
        void UseStorage(IReportHistoryStorage storage, StorageReplaceMode mode);
    }
}
