using Ohirok.PluginChains.Common;

namespace Ohirok.Reporting.History
{
    // Registered as IReportHistoryStorage unconditionally so the CustomStorage plugin (which has no
    // storageFactory) can hand a real implementation to an already-resolvable service via UseStorage,
    // mirroring Ohirok.PersistentCleanup's ICleanupStorageManager pattern.
    internal sealed class ReportHistoryStorageProxy : IReportHistoryStorage, IReportHistoryStorageManager
    {
        private readonly LateBoundStorageProxy<IReportHistoryStorage> _proxy;

        public ReportHistoryStorageProxy(IReportHistoryStorage inner = null)
        {
            _proxy = new LateBoundStorageProxy<IReportHistoryStorage>(inner);
        }

        public void UseStorage(IReportHistoryStorage storage, StorageReplaceMode mode) => _proxy.UseStorage(storage, mode);

        public Task InitializeAsync(CancellationToken ct = default) =>
            _proxy.Current?.InitializeAsync(ct) ?? Task.CompletedTask;

        public Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default) =>
            _proxy.Current?.SaveRunAsync(snapshot, ct) ?? Task.CompletedTask;

        public Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default) =>
            _proxy.Current?.GetRecentRunsAsync(count, ct) ?? Task.FromResult<IReadOnlyList<ReportHistoryRunSnapshot>>([]);
    }
}
