using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

namespace Ohirok.Reporting.History.CosmosDb.ReqnrollPlugin
{
    public sealed class CosmosDbHistoryStorage : IReportHistoryStorage
    {
        private readonly CosmosClient _client;
        private readonly string _databaseId;
        private readonly string _containerId;
        private Container _container;

        public CosmosDbHistoryStorage(
            string connectionString,
            string databaseId = "ReportHistoryDb",
            string containerId = "ReportHistoryRuns")
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _client = new CosmosClient(connectionString);
            _databaseId = databaseId;
            _containerId = containerId;
        }

        // Each run document is its own partition (no natural per-run grouping key exists once a document
        // holds N scenarios) - see this plugin's README for the resulting cross-partition query tradeoff.
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            var database = await _client.CreateDatabaseIfNotExistsAsync(_databaseId, cancellationToken: ct);
            var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
                _containerId,
                "/id",
                cancellationToken: ct);

            _container = containerResponse.Container;
        }

        /// <summary>Upserts by RunId (Option A): when <paramref name="snapshot"/>.RunId is
        /// non-null and a document with the same RunId already exists, the two are merged (see
        /// <see cref="ReportHistorySnapshotMerger"/>) and that document is replaced in its own existing
        /// partition (reusing its own <c>id</c>, which is also the partition key here) instead of a
        /// second document being created. The lookup is a cross-partition query (see the
        /// <see cref="GetRecentRunsAsync"/> comment below) - acceptable because it only runs once per
        /// formatter-process save, not per read.</summary>
        public async Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default)
        {
            CosmosHistoryRun? existing = null;

            if (snapshot.RunId != null)
            {
                var query = _container.GetItemLinqQueryable<CosmosHistoryRun>()
                    .Where(r => r.RunId == snapshot.RunId)
                    .Take(1)
                    .ToFeedIterator();

                if (query.HasMoreResults)
                {
                    var response = await query.ReadNextAsync(ct);
                    existing = response.FirstOrDefault();
                }
            }

            if (existing != null)
            {
                var merged = ReportHistorySnapshotMerger.Merge(FromCosmosRun(existing), snapshot);
                var cosmosRun = ToCosmosRun(merged);
                cosmosRun.id = existing.id;

                await _container.ReplaceItemAsync(
                    cosmosRun,
                    cosmosRun.id,
                    new PartitionKey(cosmosRun.id),
                    cancellationToken: ct);
            }
            else
            {
                var cosmosRun = ToCosmosRun(snapshot);
                await _container.CreateItemAsync(
                    cosmosRun,
                    new PartitionKey(cosmosRun.id),
                    cancellationToken: ct);
            }
        }

        // No partition-key filter is available (each run is its own partition), so this is inherently a
        // cross-partition query - see this plugin's README.
        public async Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            var query = _container.GetItemLinqQueryable<CosmosHistoryRun>()
                .OrderByDescending(r => r.RunAt)
                .Take(count)
                .ToFeedIterator();

            var runs = new List<ReportHistoryRunSnapshot>();
            while (query.HasMoreResults)
            {
                var response = await query.ReadNextAsync(ct);
                runs.AddRange(response.Select(FromCosmosRun));
            }

            return runs;
        }

        private static CosmosHistoryRun ToCosmosRun(ReportHistoryRunSnapshot snapshot)
        {
            return new CosmosHistoryRun
            {
                id = Guid.NewGuid().ToString(),
                RunAt = snapshot.RunAt.UtcDateTime,
                RunId = snapshot.RunId,
                RunUrl = snapshot.RunUrl,
                Scenarios = snapshot.Scenarios.Select(s => new CosmosHistoryScenarioOutcome
                {
                    ScenarioKey = s.ScenarioKey,
                    ScenarioName = s.ScenarioName,
                    Outcome = (int)s.Outcome
                }).ToList()
            };
        }

        private static ReportHistoryRunSnapshot FromCosmosRun(CosmosHistoryRun cosmosRun)
        {
            return new ReportHistoryRunSnapshot
            {
                RunAt = new DateTimeOffset(cosmosRun.RunAt, TimeSpan.Zero),
                RunId = cosmosRun.RunId,
                RunUrl = cosmosRun.RunUrl,
                Scenarios = cosmosRun.Scenarios.Select(s => new ReportHistoryScenarioOutcome
                {
                    ScenarioKey = s.ScenarioKey,
                    ScenarioName = s.ScenarioName,
                    Outcome = (RunOutcome)s.Outcome
                }).ToList()
            };
        }
    }

    internal class CosmosHistoryRun
    {
        public string id { get; set; } // Cosmos DB requires lowercase 'id'; also this run's partition key.
        public DateTime RunAt { get; set; }
        public string? RunId { get; set; }
        public string? RunUrl { get; set; }
        public List<CosmosHistoryScenarioOutcome> Scenarios { get; set; } = new List<CosmosHistoryScenarioOutcome>();
    }

    internal class CosmosHistoryScenarioOutcome
    {
        public string ScenarioKey { get; set; }
        public string ScenarioName { get; set; }
        public int Outcome { get; set; }
    }
}
