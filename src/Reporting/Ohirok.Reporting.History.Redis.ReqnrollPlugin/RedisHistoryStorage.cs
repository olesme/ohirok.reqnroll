using System.Text.Json;
using StackExchange.Redis;

namespace Ohirok.Reporting.History.Redis.ReqnrollPlugin
{
    public sealed class RedisHistoryStorage : IReportHistoryStorage, IDisposable
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _db;
        private readonly string _runsKey;

        public RedisHistoryStorage(string connectionString, string keyPrefix = "reporthistory:", int database = 0)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _redis = ConnectionMultiplexer.Connect(connectionString);
            _db = _redis.GetDatabase(database);
            _runsKey = $"{keyPrefix}runs";
        }

        public Task InitializeAsync(CancellationToken ct = default)
        {
            // No schema to create in Redis; sorted-set members are self-describing.
            return Task.CompletedTask;
        }

        /// <summary>Upserts by RunId (Option A): when <paramref name="snapshot"/>.RunId is
        /// non-null and an existing sorted-set member has the same RunId, the two are merged (see
        /// <see cref="ReportHistorySnapshotMerger"/>); the old member is removed and the merged member
        /// is added back under the merged (= existing) RunAt's score. Redis sorted sets have no
        /// secondary index, so finding the match requires scanning every member - acceptable here
        /// because it only runs once per formatter-process save, not per read (<see
        /// cref="GetRecentRunsAsync"/> stays a cheap bounded-rank range read).</summary>
        public async Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default)
        {
            RedisValue? existingMember = null;
            ReportHistoryRunSnapshot? existingSnapshot = null;

            if (snapshot.RunId != null)
            {
                var allMembers = await _db.SortedSetRangeByRankAsync(_runsKey);
                foreach (var member in allMembers)
                {
                    var candidate = JsonSerializer.Deserialize<RedisHistoryRun>(member.ToString());
                    if (candidate.RunId == snapshot.RunId)
                    {
                        existingMember = member;
                        existingSnapshot = FromRedisRun(candidate);
                        break;
                    }
                }
            }

            var merged = existingSnapshot != null
                ? ReportHistorySnapshotMerger.Merge(existingSnapshot, snapshot)
                : snapshot;

            var payload = ToRedisRun(merged);
            var json = JsonSerializer.Serialize(payload);

            if (existingMember != null)
            {
                var batch = _db.CreateBatch();
                var removeTask = batch.SortedSetRemoveAsync(_runsKey, existingMember.Value);
                var addTask = batch.SortedSetAddAsync(_runsKey, json, merged.RunAt.UtcDateTime.Ticks);
                batch.Execute();
                await Task.WhenAll(removeTask, addTask);
            }
            else
            {
                await _db.SortedSetAddAsync(_runsKey, json, merged.RunAt.UtcDateTime.Ticks);
            }
        }

        private static RedisHistoryRun ToRedisRun(ReportHistoryRunSnapshot snapshot)
        {
            return new RedisHistoryRun
            {
                RunAt = snapshot.RunAt.UtcDateTime.ToString("o"),
                RunId = snapshot.RunId,
                RunUrl = snapshot.RunUrl,
                Scenarios = [.. snapshot.Scenarios.Select(s => new RedisHistoryScenarioOutcome
                {
                    ScenarioKey = s.ScenarioKey,
                    ScenarioName = s.ScenarioName,
                    Outcome = (int)s.Outcome
                })]
            };
        }

        private static ReportHistoryRunSnapshot FromRedisRun(RedisHistoryRun payload)
        {
            return new ReportHistoryRunSnapshot
            {
                RunAt = DateTimeOffset.Parse(payload.RunAt),
                RunId = payload.RunId,
                RunUrl = payload.RunUrl,
                Scenarios = [.. payload.Scenarios.Select(s => new ReportHistoryScenarioOutcome
                {
                    ScenarioKey = s.ScenarioKey,
                    ScenarioName = s.ScenarioName,
                    Outcome = (RunOutcome)s.Outcome
                })]
            };
        }

        public async Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            var entries = await _db.SortedSetRangeByRankAsync(_runsKey, 0, count - 1, Order.Descending);

            var runs = new List<ReportHistoryRunSnapshot>();
            foreach (var entry in entries)
            {
                var payload = JsonSerializer.Deserialize<RedisHistoryRun>(entry.ToString());
                runs.Add(FromRedisRun(payload));
            }

            return runs;
        }

        public void Dispose()
        {
            _redis?.Dispose();
        }

        private sealed class RedisHistoryRun
        {
            public string RunAt { get; set; }
            public string? RunId { get; set; }
            public string? RunUrl { get; set; }
            public List<RedisHistoryScenarioOutcome> Scenarios { get; set; } = [];
        }

        private sealed class RedisHistoryScenarioOutcome
        {
            public string ScenarioKey { get; set; }
            public string ScenarioName { get; set; }
            public int Outcome { get; set; }
        }
    }
}
