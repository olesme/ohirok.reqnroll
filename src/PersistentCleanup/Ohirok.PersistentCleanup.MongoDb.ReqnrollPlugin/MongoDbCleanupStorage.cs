using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Ohirok.PersistentCleanup.MongoDb.ReqnrollPlugin
{
    public sealed class MongoDbCleanupStorage : ICleanupStorage
    {
        private readonly IMongoCollection<MongoCleanupRecord> _collection;

        public MongoDbCleanupStorage(
            string connectionString,
            string databaseName = "CleanupDb",
            string collectionName = "CleanupRecords")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            var client = new MongoClient(connectionString);
            _collection = client.GetDatabase(databaseName).GetCollection<MongoCleanupRecord>(collectionName);
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            var keys = Builders<MongoCleanupRecord>.IndexKeys;

            await _collection.Indexes.CreateManyAsync(new[]
            {
                new CreateIndexModel<MongoCleanupRecord>(keys.Ascending(r => r.Status).Ascending(r => r.Scope)),
                new CreateIndexModel<MongoCleanupRecord>(keys.Ascending(r => r.Order).Ascending(r => r.CreatedAt))
            }, cancellationToken: ct);
        }

        public Task SaveAsync(CleanupRecord record, CancellationToken ct = default) =>
            _collection.InsertOneAsync(ToMongo(record), cancellationToken: ct);

        public async Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default)
        {
            if (statuses == null || statuses.Count == 0)
                return new List<CleanupRecord>();

            var f = Builders<MongoCleanupRecord>.Filter;
            var filter = f.In(r => r.Status, statuses.Select(s => (int)s));
            if (scope != null)
                filter &= f.Eq(r => r.Scope, scope);

            var records = await _collection.Find(filter)
                .SortBy(r => r.Order).ThenBy(r => r.CreatedAt)
                .ToListAsync(ct);

            return records.Select(FromMongo).ToList();
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            var record = await _collection.Find(r => r.Id == id).FirstOrDefaultAsync(ct);
            return record == null ? null : FromMongo(record);
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            var f = Builders<MongoCleanupRecord>.Filter;
            var filter = f.Eq(r => r.Id, id) & f.Eq(r => r.Status, (int)expectedStatus);

            if (lastAttemptBefore.HasValue)
                filter &= f.Or(
                    f.Eq(r => r.LastAttemptAt, null),
                    f.Lt(r => r.LastAttemptAt, lastAttemptBefore.Value));

            var update = Builders<MongoCleanupRecord>.Update
                .Set(r => r.Status, (int)CleanupStatus.Processing)
                .Set(r => r.LastAttemptAt, DateTime.UtcNow);

            // A single conditional update is atomic: only one of the competing callers modifies the document.
            var result = await _collection.UpdateOneAsync(filter, update, cancellationToken: ct);
            return result.ModifiedCount > 0;
        }

        public async Task UpdateStatusAsync(
            string id,
            CleanupStatus status,
            string errorMessage = null,
            int? attemptCount = null,
            CancellationToken ct = default)
        {
            var update = Builders<MongoCleanupRecord>.Update
                .Set(r => r.Status, (int)status)
                .Set(r => r.LastAttemptAt, DateTime.UtcNow)
                .Set(r => r.ErrorMessage, errorMessage);

            if (attemptCount.HasValue)
                update = update.Set(r => r.AttemptCount, attemptCount.Value);

            await _collection.UpdateOneAsync(r => r.Id == id, update, cancellationToken: ct);
        }

        public Task DeleteAsync(string id, CancellationToken ct = default) =>
            _collection.DeleteOneAsync(r => r.Id == id, ct);

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            var f = Builders<MongoCleanupRecord>.Filter;
            var filter = FilterDefinition<MongoCleanupRecord>.Empty;

            if (olderThan.HasValue) filter &= f.Lt(r => r.CreatedAt, olderThan.Value);
            if (status.HasValue) filter &= f.Eq(r => r.Status, (int)status.Value);
            if (scope != null) filter &= f.Eq(r => r.Scope, scope);

            var result = await _collection.DeleteManyAsync(filter, ct);
            return (int)result.DeletedCount;
        }

        private static MongoCleanupRecord ToMongo(CleanupRecord r) => new MongoCleanupRecord
        {
            Id = r.Id,
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

        private static CleanupRecord FromMongo(MongoCleanupRecord r) => new CleanupRecord
        {
            Id = r.Id,
            Scope = r.Scope ?? string.Empty,
            TypeName = r.TypeName,
            MethodKey = r.MethodKey,
            Signature = r.Signature,
            ArgumentsJson = r.ArgumentsJson,
            Description = r.Description,
            CreatedAt = r.CreatedAt,
            Status = (CleanupStatus)r.Status,
            LastAttemptAt = r.LastAttemptAt,
            AttemptCount = r.AttemptCount,
            MaxRetries = r.MaxRetries,
            ErrorMessage = r.ErrorMessage,
            Order = r.Order
        };
    }

    internal class MongoCleanupRecord
    {
        [BsonId]
        public string Id { get; set; }

        public string Scope { get; set; }
        public string TypeName { get; set; }
        public string MethodKey { get; set; }
        public string Signature { get; set; }
        public string ArgumentsJson { get; set; }
        public string Description { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAt { get; set; }

        public int Status { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? LastAttemptAt { get; set; }

        public int AttemptCount { get; set; }
        public int MaxRetries { get; set; }
        public string ErrorMessage { get; set; }
        public int Order { get; set; }
    }
}
