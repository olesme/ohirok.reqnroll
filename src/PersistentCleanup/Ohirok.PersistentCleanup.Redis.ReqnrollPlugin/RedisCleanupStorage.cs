using System.Text.Json;
using StackExchange.Redis;

namespace Ohirok.PersistentCleanup.Redis.ReqnrollPlugin
{
    /// <summary>
    /// Layout: one string key per record (JSON), one set of ids per status, one set of all ids.
    /// Claiming is a Redis transaction with a condition on the exact stored value (compare-and-set).
    /// </summary>
    public sealed class RedisCleanupStorage : ICleanupStorage, IDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly string _keyPrefix;

        public RedisCleanupStorage(string connectionString, string keyPrefix = "cleanup:", int database = 0)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase(database);
            _keyPrefix = keyPrefix;
        }

        public Task InitializeAsync(CancellationToken ct = default) => Task.CompletedTask; // no schema in Redis

        public async Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            record.Scope ??= string.Empty;

            await _db.StringSetAsync(RecordKey(record.Id), JsonSerializer.Serialize(record));
            await _db.SetAddAsync(StatusKey(record.Status), record.Id);
            await _db.SetAddAsync(AllKey, record.Id);
        }

        public async Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default)
        {
            var records = new List<CleanupRecord>();
            if (statuses == null)
                return records;

            foreach (var status in statuses)
            {
                foreach (var id in await _db.SetMembersAsync(StatusKey(status)))
                {
                    var record = await GetByIdAsync(id.ToString(), ct);

                    // The record is the source of truth: the set may be momentarily out of date.
                    if (record != null && record.Status == status && (scope == null || record.Scope == scope))
                        records.Add(record);
                }
            }

            return records.OrderBy(r => r.Order).ThenBy(r => r.CreatedAt).ToList();
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var json = await _db.StringGetAsync(RecordKey(id));
            return json.HasValue ? JsonSerializer.Deserialize<CleanupRecord>(json.ToString()) : null;
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            var key = RecordKey(id);
            var raw = await _db.StringGetAsync(key);
            if (!raw.HasValue)
                return false;

            var record = JsonSerializer.Deserialize<CleanupRecord>(raw.ToString());
            if (record.Status != expectedStatus)
                return false;
            if (lastAttemptBefore.HasValue && record.LastAttemptAt.HasValue
                && record.LastAttemptAt.Value.ToUniversalTime() >= lastAttemptBefore.Value.ToUniversalTime())
                return false;

            var oldStatus = record.Status;
            record.Status = CleanupStatus.Processing;
            record.LastAttemptAt = DateTime.UtcNow;

            // Applied only if the stored value is still exactly what we read: otherwise someone else got there first.
            var tx = _db.CreateTransaction();
            tx.AddCondition(Condition.StringEqual(key, raw));
            _ = tx.StringSetAsync(key, JsonSerializer.Serialize(record));
            _ = tx.SetRemoveAsync(StatusKey(oldStatus), id);
            _ = tx.SetAddAsync(StatusKey(CleanupStatus.Processing), id);

            return await tx.ExecuteAsync();
        }

        public async Task UpdateStatusAsync(
            string id,
            CleanupStatus status,
            string errorMessage = null,
            int? attemptCount = null,
            CancellationToken ct = default)
        {
            var record = await GetByIdAsync(id, ct);
            if (record == null)
                return;

            var oldStatus = record.Status;
            record.Status = status;
            record.LastAttemptAt = DateTime.UtcNow;
            record.ErrorMessage = errorMessage;
            if (attemptCount.HasValue)
                record.AttemptCount = attemptCount.Value;

            var tx = _db.CreateTransaction();
            _ = tx.StringSetAsync(RecordKey(id), JsonSerializer.Serialize(record));
            if (oldStatus != status)
            {
                _ = tx.SetRemoveAsync(StatusKey(oldStatus), id);
                _ = tx.SetAddAsync(StatusKey(status), id);
            }

            await tx.ExecuteAsync();
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            var record = await GetByIdAsync(id, ct);

            var tx = _db.CreateTransaction();
            _ = tx.KeyDeleteAsync(RecordKey(id));
            _ = tx.SetRemoveAsync(AllKey, id);
            if (record != null)
                _ = tx.SetRemoveAsync(StatusKey(record.Status), id);

            await tx.ExecuteAsync();
        }

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            var ids = status.HasValue
                ? await _db.SetMembersAsync(StatusKey(status.Value))
                : await _db.SetMembersAsync(AllKey);

            var deleted = 0;
            foreach (var id in ids)
            {
                var record = await GetByIdAsync(id.ToString(), ct);
                if (record == null)
                    continue;
                if (status.HasValue && record.Status != status.Value)
                    continue;
                if (scope != null && record.Scope != scope)
                    continue;
                if (olderThan.HasValue && record.CreatedAt.ToUniversalTime() >= olderThan.Value.ToUniversalTime())
                    continue;

                await DeleteAsync(record.Id, ct);
                deleted++;
            }

            return deleted;
        }

        private string RecordKey(string id) => $"{_keyPrefix}record:{id}";
        private string StatusKey(CleanupStatus status) => $"{_keyPrefix}status:{(int)status}";
        private string AllKey => $"{_keyPrefix}all";

        public void Dispose() => _redis?.Dispose();
    }
}
