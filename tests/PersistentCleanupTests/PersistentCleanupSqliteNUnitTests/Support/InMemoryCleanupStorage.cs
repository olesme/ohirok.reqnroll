using Ohirok.PersistentCleanup;

namespace PersistentCleanupSqliteNUnitTests.Support
{
    /// <summary>
    /// Thread-safe in-memory <see cref="ICleanupStorage"/>. Implements every contract rule, including the
    /// atomic <see cref="TryClaimAsync"/>, so it can be used as a reference for custom storages.
    /// </summary>
    public class InMemoryCleanupStorage : ICleanupStorage
    {
        private readonly object _lock = new();
        private readonly List<CleanupRecord> _records = [];

        /// <summary>A snapshot copy of all records (changing it does not change the storage).</summary>
        public IReadOnlyList<CleanupRecord> Records
        {
            get { lock (_lock) return _records.Select(Clone).ToList(); }
        }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            lock (_lock) _records.Add(Clone(record));
            return Task.CompletedTask;
        }

        public Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses, string? scope = null, CancellationToken ct = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_records
                    .Where(r => statuses.Contains(r.Status) && (scope == null || r.Scope == scope))
                    .OrderBy(r => r.Order).ThenBy(r => r.CreatedAt)
                    .Select(Clone)
                    .ToList());
            }
        }

        public Task<CleanupRecord?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            lock (_lock)
            {
                var record = _records.FirstOrDefault(r => r.Id == id);
                return Task.FromResult(record == null ? null : Clone(record));
            }
        }

        public Task<bool> TryClaimAsync(
            string id, CleanupStatus expectedStatus, DateTime? lastAttemptBefore, CancellationToken ct = default)
        {
            lock (_lock)
            {
                var record = _records.FirstOrDefault(r => r.Id == id);
                if (record == null || record.Status != expectedStatus)
                    return Task.FromResult(false);

                if (lastAttemptBefore.HasValue && record.LastAttemptAt.HasValue
                    && record.LastAttemptAt.Value >= lastAttemptBefore.Value)
                    return Task.FromResult(false);

                record.Status = CleanupStatus.Processing;
                record.LastAttemptAt = DateTime.UtcNow;
                return Task.FromResult(true);
            }
        }

        public Task UpdateStatusAsync(
            string id, CleanupStatus status, string? errorMessage = null, int? attemptCount = null,
            CancellationToken ct = default)
        {
            lock (_lock)
            {
                var record = _records.FirstOrDefault(r => r.Id == id);
                if (record != null)
                {
                    record.Status = status;
                    record.LastAttemptAt = DateTime.UtcNow;
                    record.ErrorMessage = errorMessage;
                    if (attemptCount.HasValue) record.AttemptCount = attemptCount.Value;
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken ct = default)
        {
            lock (_lock) _records.RemoveAll(r => r.Id == id);
            return Task.CompletedTask;
        }

        public Task<int> DeleteWhereAsync(
            DateTime? olderThan, CleanupStatus? status, string? scope, CancellationToken ct = default)
        {
            lock (_lock)
            {
                return Task.FromResult(_records.RemoveAll(r =>
                    (!olderThan.HasValue || r.CreatedAt < olderThan.Value)
                    && (!status.HasValue || r.Status == status.Value)
                    && (scope == null || r.Scope == scope)));
            }
        }

        /// <summary>Test helper: adds a record exactly as given (e.g. with an old LastAttemptAt).</summary>
        public void Seed(CleanupRecord record)
        {
            lock (_lock) _records.Add(Clone(record));
        }

        private static CleanupRecord Clone(CleanupRecord r) => new()
        {
            Id = r.Id, Scope = r.Scope, TypeName = r.TypeName, MethodKey = r.MethodKey, Signature = r.Signature,
            ArgumentsJson = r.ArgumentsJson, Description = r.Description, CreatedAt = r.CreatedAt, Status = r.Status,
            LastAttemptAt = r.LastAttemptAt, AttemptCount = r.AttemptCount, MaxRetries = r.MaxRetries,
            ErrorMessage = r.ErrorMessage, Order = r.Order
        };
    }
}
