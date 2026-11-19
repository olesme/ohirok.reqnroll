using System.Net;
using Microsoft.Azure.Cosmos;

namespace Ohirok.PersistentCleanup.CosmosDb.ReqnrollPlugin
{
    /// <summary>
    /// The container is partitioned by "/id", so a status change is an in-place replace (not delete + insert)
    /// and a claim is a replace guarded by the item's ETag. Listing queries are cross-partition, which is fine
    /// for a table that only holds leftover cleanups.
    /// </summary>
    public sealed class CosmosDbCleanupStorage : ICleanupStorage
    {
        private readonly CosmosClient _client;
        private readonly string _databaseId;
        private readonly string _containerId;
        private Container _container;

        public CosmosDbCleanupStorage(
            string connectionString,
            string databaseId = "CleanupDb",
            string containerId = "CleanupRecords")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _client = new CosmosClient(connectionString);
            _databaseId = databaseId;
            _containerId = containerId;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            var database = await _client.CreateDatabaseIfNotExistsAsync(_databaseId, cancellationToken: ct);
            var container = await database.Database.CreateContainerIfNotExistsAsync(_containerId, "/id", cancellationToken: ct);
            _container = container.Container;
        }

        public async Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            await GetContainer().CreateItemAsync(ToCosmos(record), new PartitionKey(record.Id), cancellationToken: ct);
        }

        public async Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default)
        {
            if (statuses == null || statuses.Count == 0)
                return new List<CleanupRecord>();

            var statusList = string.Join(",", statuses.Select(s => (int)s)); // integers only
            var sql = $"SELECT * FROM c WHERE c.Status IN ({statusList})";
            if (scope != null)
                sql += " AND c.Scope = @scope";

            var query = new QueryDefinition(sql);
            if (scope != null)
                query = query.WithParameter("@scope", scope);

            var records = await ReadAllAsync(query, ct);
            return records.Select(FromCosmos)
                .OrderBy(r => r.Order).ThenBy(r => r.CreatedAt)
                .ToList();
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var item = await TryReadAsync(id, ct);
            return item == null ? null : FromCosmos(item.Value.Item);
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            var read = await TryReadAsync(id, ct);
            if (read == null)
                return false;

            var item = read.Value.Item;
            if (item.Status != (int)expectedStatus)
                return false;
            if (lastAttemptBefore.HasValue && item.LastAttemptAt.HasValue
                && item.LastAttemptAt.Value.ToUniversalTime() >= lastAttemptBefore.Value.ToUniversalTime())
                return false;

            item.Status = (int)CleanupStatus.Processing;
            item.LastAttemptAt = DateTime.UtcNow;

            try
            {
                // Succeeds only if nobody changed the item since we read it.
                await GetContainer().ReplaceItemAsync(
                    item, id, new PartitionKey(id),
                    new ItemRequestOptions { IfMatchEtag = read.Value.ETag },
                    ct);
                return true;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed
                                             || ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public async Task UpdateStatusAsync(
            string id,
            CleanupStatus status,
            string errorMessage = null,
            int? attemptCount = null,
            CancellationToken ct = default)
        {
            var read = await TryReadAsync(id, ct);
            if (read == null)
                return;

            var item = read.Value.Item;
            item.Status = (int)status;
            item.LastAttemptAt = DateTime.UtcNow;
            item.ErrorMessage = errorMessage;
            if (attemptCount.HasValue)
                item.AttemptCount = attemptCount.Value;

            await GetContainer().ReplaceItemAsync(item, id, new PartitionKey(id), cancellationToken: ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            try
            {
                await GetContainer().DeleteItemAsync<CosmosCleanupRecord>(id, new PartitionKey(id), cancellationToken: ct);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                // already gone
            }
        }

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            var conditions = new List<string>();
            if (status.HasValue) conditions.Add("c.Status = @status");
            if (scope != null) conditions.Add("c.Scope = @scope");

            var sql = "SELECT * FROM c" + (conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "");
            var query = new QueryDefinition(sql);
            if (status.HasValue) query = query.WithParameter("@status", (int)status.Value);
            if (scope != null) query = query.WithParameter("@scope", scope);

            var deleted = 0;
            foreach (var item in await ReadAllAsync(query, ct))
            {
                // Compared in memory: DateTime serialization differences make a server-side comparison fragile.
                if (olderThan.HasValue && item.CreatedAt.ToUniversalTime() >= olderThan.Value.ToUniversalTime())
                    continue;

                await DeleteAsync(item.id, ct);
                deleted++;
            }

            return deleted;
        }

        private Container GetContainer() =>
            _container ?? throw new InvalidOperationException("The storage is not initialized. Call InitializeAsync first.");

        private async Task<(CosmosCleanupRecord Item, string ETag)?> TryReadAsync(string id, CancellationToken ct)
        {
            try
            {
                var response = await GetContainer().ReadItemAsync<CosmosCleanupRecord>(id, new PartitionKey(id), cancellationToken: ct);
                return (response.Resource, response.ETag);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        private async Task<List<CosmosCleanupRecord>> ReadAllAsync(QueryDefinition query, CancellationToken ct)
        {
            var items = new List<CosmosCleanupRecord>();
            var iterator = GetContainer().GetItemQueryIterator<CosmosCleanupRecord>(query);
            while (iterator.HasMoreResults)
                items.AddRange(await iterator.ReadNextAsync(ct));
            return items;
        }

        private static CosmosCleanupRecord ToCosmos(CleanupRecord r) => new CosmosCleanupRecord
        {
            id = r.Id,
            Scope = r.Scope ?? string.Empty,
            TypeName = r.TypeName,
            MethodKey = r.MethodKey,
            Signature = r.Signature,
            ArgumentsJson = r.ArgumentsJson,
            Description = r.Description,
            CreatedAt = r.CreatedAt,
            Status = (int)r.Status,
            LastAttemptAt = r.LastAttemptAt,
            AttemptCount = r.AttemptCount,
            MaxRetries = r.MaxRetries,
            ErrorMessage = r.ErrorMessage,
            Order = r.Order
        };

        private static CleanupRecord FromCosmos(CosmosCleanupRecord r) => new CleanupRecord
        {
            Id = r.id,
            Scope = r.Scope ?? string.Empty,
            TypeName = r.TypeName,
            MethodKey = r.MethodKey,
            Signature = r.Signature,
            ArgumentsJson = r.ArgumentsJson,
            Description = r.Description,
            CreatedAt = r.CreatedAt.ToUniversalTime(),
            Status = (CleanupStatus)r.Status,
            LastAttemptAt = r.LastAttemptAt?.ToUniversalTime(),
            AttemptCount = r.AttemptCount,
            MaxRetries = r.MaxRetries,
            ErrorMessage = r.ErrorMessage,
            Order = r.Order
        };
    }

    internal class CosmosCleanupRecord
    {
        public string id { get; set; } // Cosmos DB requires a lowercase 'id'; it is also the partition key
        public string Scope { get; set; }
        public string TypeName { get; set; }
        public string MethodKey { get; set; }
        public string Signature { get; set; }
        public string ArgumentsJson { get; set; }
        public string Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public int Status { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public int AttemptCount { get; set; }
        public int MaxRetries { get; set; }
        public string ErrorMessage { get; set; }
        public int Order { get; set; }
    }
}
