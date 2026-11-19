using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Ohirok.Reporting.History.MongoDb.ReqnrollPlugin
{
    public sealed class MongoDbHistoryStorage : IReportHistoryStorage
    {
        private readonly IMongoClient _client;
        private readonly IMongoDatabase _database;
        private readonly IMongoCollection<MongoHistoryRun> _collection;

        public MongoDbHistoryStorage(
            string connectionString,
            string databaseName = "ReportHistoryDb",
            string collectionName = "ReportHistoryRuns")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _client = new MongoClient(connectionString);
            _database = _client.GetDatabase(databaseName);
            _collection = _database.GetCollection<MongoHistoryRun>(collectionName);
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            var runAtIndex = Builders<MongoHistoryRun>.IndexKeys.Descending(r => r.RunAt);

            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<MongoHistoryRun>(runAtIndex),
                cancellationToken: ct);

            var runIdIndex = Builders<MongoHistoryRun>.IndexKeys.Ascending(r => r.RunId);

            await _collection.Indexes.CreateOneAsync(
                new CreateIndexModel<MongoHistoryRun>(runIdIndex, new CreateIndexOptions { Sparse = true }),
                cancellationToken: ct);
        }

        /// <summary>Upserts by RunId (Option A): when <paramref name="snapshot"/>.RunId is
        /// non-null and a document with the same RunId already exists, the two are merged (see
        /// <see cref="ReportHistorySnapshotMerger"/>) and that document is replaced in place instead of
        /// a second document being inserted. No multi-document transaction is used here - a standalone
        /// (non-replica-set) MongoDB server can't start one, and both the find and the replace are each
        /// individually atomic at the single-document level, which is enough for this storage's actual
        /// usage pattern (one formatter process writing its own run, not concurrent writers racing on
        /// the same RunId).</summary>
        public async Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default)
        {
            MongoHistoryRun? existing = null;

            if (snapshot.RunId != null)
            {
                var filter = Builders<MongoHistoryRun>.Filter.Eq(r => r.RunId, snapshot.RunId);
                existing = await _collection.Find(filter).FirstOrDefaultAsync(ct);
            }

            if (existing != null)
            {
                var merged = ReportHistorySnapshotMerger.Merge(FromMongoRun(existing), snapshot);
                var mongoRun = ToMongoRun(merged);
                mongoRun._id = existing._id;

                var replaceFilter = Builders<MongoHistoryRun>.Filter.Eq(r => r._id, existing._id);
                await _collection.ReplaceOneAsync(replaceFilter, mongoRun, cancellationToken: ct);
            }
            else
            {
                var mongoRun = ToMongoRun(snapshot);
                await _collection.InsertOneAsync(mongoRun, cancellationToken: ct);
            }
        }

        public async Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            var mongoRuns = await _collection
                .Find(FilterDefinition<MongoHistoryRun>.Empty)
                .SortByDescending(r => r.RunAt)
                .Limit(count)
                .ToListAsync(ct);

            return [.. mongoRuns.Select(FromMongoRun)];
        }

        private static MongoHistoryRun ToMongoRun(ReportHistoryRunSnapshot snapshot)
        {
            return new MongoHistoryRun
            {
                RunAt = snapshot.RunAt.UtcDateTime,
                RunId = snapshot.RunId,
                RunUrl = snapshot.RunUrl,
                Scenarios = snapshot.Scenarios.Select(s => new MongoHistoryScenarioOutcome
                {
                    ScenarioKey = s.ScenarioKey,
                    ScenarioName = s.ScenarioName,
                    Outcome = (int)s.Outcome
                }).ToList()
            };
        }

        private static ReportHistoryRunSnapshot FromMongoRun(MongoHistoryRun mongoRun)
        {
            return new ReportHistoryRunSnapshot
            {
                RunAt = new DateTimeOffset(mongoRun.RunAt, TimeSpan.Zero),
                RunId = mongoRun.RunId,
                RunUrl = mongoRun.RunUrl,
                Scenarios = mongoRun.Scenarios.Select(s => new ReportHistoryScenarioOutcome
                {
                    ScenarioKey = s.ScenarioKey,
                    ScenarioName = s.ScenarioName,
                    Outcome = (RunOutcome)s.Outcome
                }).ToList()
            };
        }
    }

    internal class MongoHistoryRun
    {
        [BsonId]
        public ObjectId _id { get; set; }

        public DateTime RunAt { get; set; }
        public string? RunId { get; set; }
        public string? RunUrl { get; set; }
        public List<MongoHistoryScenarioOutcome> Scenarios { get; set; } = [];
    }

    internal class MongoHistoryScenarioOutcome
    {
        public string ScenarioKey { get; set; }
        public string ScenarioName { get; set; }
        public int Outcome { get; set; }
    }
}
