using System.Data;
using System.Text.Json;
using MySqlConnector;

namespace Ohirok.Reporting.History.MySql.ReqnrollPlugin
{
    public sealed class MySqlHistoryStorage : IReportHistoryStorage
    {
        private readonly string _connectionString;
        private readonly bool _autoCreate;

        public MySqlHistoryStorage(string connectionString, bool autoCreate = true)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _connectionString = connectionString;
            _autoCreate = autoCreate;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_autoCreate) return;

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var createTableSql = @"
                CREATE TABLE IF NOT EXISTS ReportHistoryRuns (
                    Id INT AUTO_INCREMENT PRIMARY KEY,
                    RunAt VARCHAR(50) NOT NULL,
                    RunId VARCHAR(100) NULL,
                    ScenariosJson LONGTEXT NOT NULL,
                    INDEX IX_ReportHistoryRuns_RunAt (RunAt)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            using var command = new MySqlCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(ct);

            // information_schema check rather than "ADD COLUMN IF NOT EXISTS" (MySQL 8.0.29+ only) to retrofit
            // a table created before RunId/RunUrl existed on any supported MySQL version.
            await AddColumnIfMissingAsync(connection, "RunId", ct);
            await AddColumnIfMissingAsync(connection, "RunUrl", ct);
        }

        private static async Task AddColumnIfMissingAsync(MySqlConnection connection, string columnName, CancellationToken ct)
        {
            var checkColumnSql = @"
                SELECT COUNT(*) FROM information_schema.columns
                WHERE table_schema = DATABASE() AND table_name = 'ReportHistoryRuns' AND column_name = @ColumnName
            ";
            using var checkColumnCommand = new MySqlCommand(checkColumnSql, connection);
            checkColumnCommand.Parameters.AddWithValue("@ColumnName", columnName);
            var columnExists = Convert.ToInt32(await checkColumnCommand.ExecuteScalarAsync(ct)) > 0;
            if (!columnExists)
            {
                using var addColumnCommand = new MySqlCommand($"ALTER TABLE ReportHistoryRuns ADD COLUMN {columnName} VARCHAR(500) NULL;", connection);
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
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            using var transaction = await connection.BeginTransactionAsync(ct);

            long? existingId = null;
            ReportHistoryRunSnapshot? existingSnapshot = null;

            if (snapshot.RunId != null)
            {
                var selectSql = @"
                    SELECT Id, RunAt, RunId, RunUrl, ScenariosJson FROM ReportHistoryRuns
                    WHERE RunId = @RunId
                    LIMIT 1
                ";

                using var selectCommand = new MySqlCommand(selectSql, connection, transaction);
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

                using var updateCommand = new MySqlCommand(updateSql, connection, transaction);
                updateCommand.Parameters.AddWithValue("@Id", existingId.Value);
                updateCommand.Parameters.AddWithValue("@RunAt", merged.RunAt.ToString("o"));
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

                using var insertCommand = new MySqlCommand(insertSql, connection, transaction);
                insertCommand.Parameters.AddWithValue("@RunAt", snapshot.RunAt.ToString("o"));
                insertCommand.Parameters.AddWithValue("@RunId", (object?)snapshot.RunId ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@RunUrl", (object?)snapshot.RunUrl ?? DBNull.Value);
                insertCommand.Parameters.AddWithValue("@ScenariosJson", JsonSerializer.Serialize(snapshot.Scenarios));

                await insertCommand.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }

        public async Task<IReadOnlyList<ReportHistoryRunSnapshot>> GetRecentRunsAsync(int count, CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = @"
                SELECT RunAt, RunId, RunUrl, ScenariosJson FROM ReportHistoryRuns
                ORDER BY RunAt DESC
                LIMIT @Count
            ";

            using var command = new MySqlCommand(sql, connection);
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
                Scenarios = JsonSerializer.Deserialize<List<ReportHistoryScenarioOutcome>>(reader.GetString(startOrdinal + 3)) ?? new List<ReportHistoryScenarioOutcome>()
            };
        }
    }
}
