using System.Data;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace Ohirok.Reporting.History.Sqlite.ReqnrollPlugin
{
    public sealed class SqliteHistoryStorage : IReportHistoryStorage
    {
        private readonly string _connectionString;
        private readonly bool _autoCreate;

        public SqliteHistoryStorage(string databasePath, bool autoCreate = true)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path cannot be empty", nameof(databasePath));

            var fullPath = Path.GetFullPath(databasePath);
            _connectionString = $"Data Source={fullPath}";
            _autoCreate = autoCreate;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_autoCreate) return;

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS ReportHistoryRuns (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RunAt TEXT NOT NULL,
                    RunId TEXT NULL,
                    ScenariosJson TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_ReportHistoryRuns_RunAt
                    ON ReportHistoryRuns(RunAt DESC);
            ";

            using var command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(ct);

            // SQLite has no "ADD COLUMN IF NOT EXISTS" - retrofit a table created before RunId/RunUrl existed.
            await AddColumnIfMissingAsync(connection, "RunId", ct);
            await AddColumnIfMissingAsync(connection, "RunUrl", ct);
        }

        private static async Task AddColumnIfMissingAsync(SqliteConnection connection, string columnName, CancellationToken ct)
        {
            using var checkColumnCommand = new SqliteCommand(
                $"SELECT COUNT(*) FROM pragma_table_info('ReportHistoryRuns') WHERE name = '{columnName}'", connection);
            var columnExists = Convert.ToInt32(await checkColumnCommand.ExecuteScalarAsync(ct)) > 0;
            if (!columnExists)
            {
                using var addColumnCommand = new SqliteCommand($"ALTER TABLE ReportHistoryRuns ADD COLUMN {columnName} TEXT NULL;", connection);
                await addColumnCommand.ExecuteNonQueryAsync(ct);
            }
        }

        /// <summary>Upserts by RunId (Option A): when <paramref name="snapshot"/>.RunId is
        /// non-null and a row with the same RunId already exists, the two are merged (see
        /// <see cref="ReportHistorySnapshotMerger"/>) and that row is UPDATEd in place instead of a second
        /// row being INSERTed. The SELECT-merge-UPDATE/INSERT sequence runs inside one transaction so a
        /// concurrent writer can't interleave between the lookup and the write.</summary>
        public async Task SaveRunAsync(ReportHistoryRunSnapshot snapshot, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var transaction = connection.BeginTransaction();

            long? existingId = null;
            ReportHistoryRunSnapshot? existingSnapshot = null;

            if (snapshot.RunId != null)
            {
                var selectSql = @"
                    SELECT Id, RunAt, RunId, RunUrl, ScenariosJson FROM ReportHistoryRuns
                    WHERE RunId = @RunId
                    LIMIT 1
                ";

                using var selectCommand = new SqliteCommand(selectSql, connection, transaction);
                selectCommand.Parameters.AddWithValue("@RunId", snapshot.RunId);

                using var reader = await selectCommand.ExecuteReaderAsync(ct);
                if (await reader.ReadAsync(ct))
                {
                    existingId = reader.GetInt64(0);
                    existingSnapshot = MapSnapshot(reader, 1);
                }
            }

            if (existingId != null)
            {
                var merged = ReportHistorySnapshotMerger.Merge(existingSnapshot!, snapshot);

                var updateSql = @"
                    UPDATE ReportHistoryRuns
                    SET RunAt = @RunAt, RunId = @RunId, RunUrl = @RunUrl, ScenariosJson = @ScenariosJson
                    WHERE Id = @Id
                ";

                using var updateCommand = new SqliteCommand(updateSql, connection, transaction);
                updateCommand.Parameters.AddWithValue("@Id", existingId.Value);
                updateCommand.Parameters.AddWithValue("@RunAt", merged.RunAt.ToString("O"));
                updateCommand.Parameters.AddWithValue("@RunId", (object?)merged.RunId ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@RunUrl", (object?)merged.RunUrl ?? DBNull.Value);
                updateCommand.Parameters.AddWithValue("@ScenariosJson", JsonSerializer.Serialize(merged.Scenarios));

                await updateCommand.ExecuteNonQueryAsync(ct);
            }
            else
            {
                var insertSql = @"
                    INSERT INTO ReportHistoryRuns (RunAt, RunId, RunUrl, ScenariosJson)
                    VALUES (@RunAt, @RunId, @RunUrl, @ScenariosJson);
                ";

                using var insertCommand = new SqliteCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@RunAt", snapshot.RunAt.ToString("O"));
                insertCommand.Parameters.AddWithValue("@RunId", (object?)snapshot.RunId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@RunUrl", (object?)snapshot.RunUrl ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@ScenariosJson", JsonSerializer.Serialize(snapshot.Scenarios));

                await insertCommand.ExecuteNonQueryAsync(ct);
            }

            transaction.Commit();
        }

        public async Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = @"
                SELECT RunAt, RunId, RunUrl, ScenariosJson FROM ReportHistoryRuns
                ORDER BY RunAt DESC
                LIMIT @Count
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Count", count);

            var snapshots = new List<ReportHistoryRunSnapshot>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                snapshots.Add(MapSnapshot(reader));
            }

            return snapshots;
        }

        private static ReportHistoryRunSnapshot MapSnapshot(IDataReader reader, int startOrdinal = 0)
        {
            return new ReportHistoryRunSnapshot
            {
                RunAt = DateTimeOffset.Parse(reader.GetString(startOrdinal)),
                RunId = reader.IsDBNull(startOrdinal + 1) ? null : reader.GetString(startOrdinal + 1),
                RunUrl = reader.IsDBNull(startOrdinal + 2) ? null : reader.GetString(startOrdinal + 2),
                Scenarios = JsonSerializer.Deserialize<List<ReportHistoryScenarioOutcome>>(reader.GetString(startOrdinal + 3)) ?? []
            };
        }
    }
}
