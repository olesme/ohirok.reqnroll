using System.Text.Json;
using Npgsql;

namespace Ohirok.Reporting.History.PostgreSql.ReqnrollPlugin
{
    public sealed class PostgreSqlHistoryStorage : IReportHistoryStorage
    {
        private readonly string _connectionString;
        private readonly bool _autoCreate;

        public PostgreSqlHistoryStorage(string connectionString, bool autoCreate = true)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _connectionString = connectionString;
            _autoCreate = autoCreate;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_autoCreate) return;

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS ""ReportHistoryRuns"" (
                    ""Id"" SERIAL PRIMARY KEY,
                    ""RunAt"" TIMESTAMPTZ NOT NULL,
                    ""RunId"" TEXT NULL,
                    ""ScenariosJson"" TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS ""IX_ReportHistoryRuns_RunAt""
                    ON ""ReportHistoryRuns"" (""RunAt"" DESC);

                ALTER TABLE ""ReportHistoryRuns"" ADD COLUMN IF NOT EXISTS ""RunId"" TEXT NULL;
                ALTER TABLE ""ReportHistoryRuns"" ADD COLUMN IF NOT EXISTS ""RunUrl"" TEXT NULL;
            ";

            using var command = new NpgsqlCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        /// <summary>Upserts by RunId (Option A): when <paramref name="snapshot"/>.RunId is
        /// non-null and a row with the same RunId already exists, the two are merged (see
        /// <see cref="ReportHistorySnapshotMerger"/>) and that row is UPDATEd in place instead of a second
        /// row being INSERTed. The SELECT-merge-UPDATE/INSERT sequence runs inside one transaction so a
        /// concurrent writer can't interleave between the lookup and the write.</summary>
        public async Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var transaction = connection.BeginTransaction();

            int? existingId = null;
            ReportHistoryRunSnapshot? existingSnapshot = null;

            if (snapshot.RunId != null)
            {
                var selectSql = @"
                    SELECT ""Id"", ""RunAt"", ""RunId"", ""RunUrl"", ""ScenariosJson"" FROM ""ReportHistoryRuns""
                    WHERE ""RunId"" = @RunId
                    LIMIT 1
                ";

                using var selectCommand = new NpgsqlCommand(selectSql, connection, transaction);
                selectCommand.Parameters.AddWithValue("@RunId", snapshot.RunId);

                using var reader = await selectCommand.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    existingId = reader.GetInt32(0);
                    existingSnapshot = MapSnapshot(reader, 1);
                }
            }

            if (existingId != null)
            {
                var merged = ReportHistorySnapshotMerger.Merge(existingSnapshot!, snapshot);

                var updateSql = @"
                    UPDATE ""ReportHistoryRuns""
                    SET ""RunAt"" = @RunAt, ""RunId"" = @RunId, ""RunUrl"" = @RunUrl, ""ScenariosJson"" = @ScenariosJson
                    WHERE ""Id"" = @Id
                ";

                using var updateCommand = new NpgsqlCommand(updateSql, connection, transaction);
                updateCommand.Parameters.AddWithValue("@Id", existingId.Value);
                updateCommand.Parameters.AddWithValue("@RunAt", merged.RunAt);
                updateCommand.Parameters.AddWithValue("@RunId", (object?)merged.RunId ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@RunUrl", (object?)merged.RunUrl ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@ScenariosJson", JsonSerializer.Serialize(merged.Scenarios));

                await updateCommand.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var insertSql = @"
                    INSERT INTO ""ReportHistoryRuns"" (""RunAt"", ""RunId"", ""RunUrl"", ""ScenariosJson"")
                    VALUES (@RunAt, @RunId, @RunUrl, @ScenariosJson);
                ";

                using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@RunAt", snapshot.RunAt);
                insertCommand.Parameters.AddWithValue("@RunId", (object?)snapshot.RunId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@RunUrl", (object?)snapshot.RunUrl ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@ScenariosJson", JsonSerializer.Serialize(snapshot.Scenarios));

                await insertCommand.ExecuteNonQueryAsync(ct);
            }

            transaction.Commit();
        }

        public async Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = @"
                SELECT ""RunAt"", ""RunId"", ""RunUrl"", ""ScenariosJson"" FROM ""ReportHistoryRuns""
                ORDER BY ""RunAt"" DESC
                LIMIT @Count
            ";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Count", count);

            var snapshots = new List<ReportHistoryRunSnapshot>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                snapshots.Add(MapSnapshot(reader));
            }

            return snapshots;
        }

        private static ReportHistoryRunSnapshot MapSnapshot(NpgsqlDataReader reader, int startOrdinal = 0)
        {
            return new ReportHistoryRunSnapshot
            {
                RunAt = reader.GetFieldValue<DateTimeOffset>(startOrdinal),
                RunId = reader.IsDBNull(startOrdinal + 1) ? null : reader.GetString(startOrdinal + 1),
                RunUrl = reader.IsDBNull(startOrdinal + 2) ? null : reader.GetString(startOrdinal + 2),
                Scenarios = JsonSerializer.Deserialize<List<ReportHistoryScenarioOutcome>>(reader.GetString(startOrdinal + 3)) ?? []
            };
        }
    }
}
