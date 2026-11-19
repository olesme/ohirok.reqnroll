using System.Data;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Ohirok.PersistentCleanup.Sqlite.ReqnrollPlugin
{
    public sealed class SqliteCleanupStorage : ICleanupStorage
    {
        private const string Columns =
            "Id, Scope, TypeName, MethodKey, Signature, ArgumentsJson, Description, CreatedAt, Status, " +
            "LastAttemptAt, AttemptCount, MaxRetries, ErrorMessage, [Order]";

        private readonly string _connectionString;
        private readonly string _fullPath;
        private readonly bool _autoCreate;

        public SqliteCleanupStorage(string databasePath, bool autoCreate = true)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new ArgumentException("Database path cannot be empty", nameof(databasePath));

            _fullPath = Path.GetFullPath(databasePath);
            _connectionString = $"Data Source={_fullPath}";
            _autoCreate = autoCreate;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_autoCreate)
                return;

            var directory = Path.GetDirectoryName(_fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS CleanupRecords (
                    Id TEXT PRIMARY KEY,
                    Scope TEXT NOT NULL DEFAULT '',
                    TypeName TEXT NOT NULL,
                    MethodKey TEXT NOT NULL,
                    Signature TEXT NOT NULL,
                    ArgumentsJson TEXT,
                    Description TEXT,
                    CreatedAt TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    LastAttemptAt TEXT,
                    AttemptCount INTEGER NOT NULL DEFAULT 0,
                    MaxRetries INTEGER NOT NULL DEFAULT 3,
                    ErrorMessage TEXT,
                    [Order] INTEGER NOT NULL DEFAULT 10000
                );

                CREATE INDEX IF NOT EXISTS IX_CleanupRecords_Status_Scope
                    ON CleanupRecords(Status, Scope);

                CREATE INDEX IF NOT EXISTS IX_CleanupRecords_Order
                    ON CleanupRecords([Order], CreatedAt);
            ";

            using var command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = $@"
                INSERT INTO CleanupRecords ({Columns})
                VALUES (@Id, @Scope, @TypeName, @MethodKey, @Signature, @ArgumentsJson, @Description,
                        @CreatedAt, @Status, @LastAttemptAt, @AttemptCount, @MaxRetries, @ErrorMessage, @Order);
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", record.Id);
            command.Parameters.AddWithValue("@Scope", record.Scope ?? string.Empty);
            command.Parameters.AddWithValue("@TypeName", record.TypeName);
            command.Parameters.AddWithValue("@MethodKey", record.MethodKey);
            command.Parameters.AddWithValue("@Signature", record.Signature);
            command.Parameters.AddWithValue("@ArgumentsJson", Db(record.ArgumentsJson));
            command.Parameters.AddWithValue("@Description", Db(record.Description));
            command.Parameters.AddWithValue("@CreatedAt", Iso(record.CreatedAt));
            command.Parameters.AddWithValue("@Status", (int)record.Status);
            command.Parameters.AddWithValue("@LastAttemptAt", record.LastAttemptAt.HasValue ? Iso(record.LastAttemptAt.Value) : DBNull.Value);
            command.Parameters.AddWithValue("@AttemptCount", record.AttemptCount);
            command.Parameters.AddWithValue("@MaxRetries", record.MaxRetries);
            command.Parameters.AddWithValue("@ErrorMessage", Db(record.ErrorMessage));
            command.Parameters.AddWithValue("@Order", record.Order);

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default)
        {
            if (statuses == null || statuses.Count == 0)
                return new List<CleanupRecord>();

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            var statusList = string.Join(",", statuses.Select(s => (int)s)); // integers only: no injection possible
            var sql = $@"
                SELECT {Columns} FROM CleanupRecords
                WHERE Status IN ({statusList}) AND (@Scope IS NULL OR Scope = @Scope)
                ORDER BY [Order], CreatedAt
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Scope", Db(scope));

            var records = new List<CleanupRecord>();
            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                records.Add(MapRecord(reader));

            return records;
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new SqliteCommand($"SELECT {Columns} FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);

            using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? MapRecord(reader) : null;
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            // A single UPDATE is atomic: of several competing processes exactly one changes a row.
            const string sql = @"
                UPDATE CleanupRecords
                SET Status = @Processing, LastAttemptAt = @Now
                WHERE Id = @Id AND Status = @Expected
                  AND (@Before IS NULL OR LastAttemptAt IS NULL OR LastAttemptAt < @Before)
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Processing", (int)CleanupStatus.Processing);
            command.Parameters.AddWithValue("@Now", Iso(DateTime.UtcNow));
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Expected", (int)expectedStatus);
            command.Parameters.AddWithValue("@Before", lastAttemptBefore.HasValue ? Iso(lastAttemptBefore.Value) : DBNull.Value);

            return await command.ExecuteNonQueryAsync(ct) > 0;
        }

        public async Task UpdateStatusAsync(
            string id,
            CleanupStatus status,
            string errorMessage = null,
            int? attemptCount = null,
            CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                UPDATE CleanupRecords
                SET Status = @Status,
                    LastAttemptAt = @Now,
                    ErrorMessage = @ErrorMessage,
                    AttemptCount = COALESCE(@AttemptCount, AttemptCount)
                WHERE Id = @Id
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Status", (int)status);
            command.Parameters.AddWithValue("@Now", Iso(DateTime.UtcNow));
            command.Parameters.AddWithValue("@ErrorMessage", Db(errorMessage));
            command.Parameters.AddWithValue("@AttemptCount", attemptCount.HasValue ? attemptCount.Value : DBNull.Value);

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new SqliteCommand("DELETE FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                DELETE FROM CleanupRecords
                WHERE (@OlderThan IS NULL OR CreatedAt < @OlderThan)
                  AND (@Status IS NULL OR Status = @Status)
                  AND (@Scope IS NULL OR Scope = @Scope)
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@OlderThan", olderThan.HasValue ? Iso(olderThan.Value) : DBNull.Value);
            command.Parameters.AddWithValue("@Status", status.HasValue ? (int)status.Value : DBNull.Value);
            command.Parameters.AddWithValue("@Scope", Db(scope));

            return await command.ExecuteNonQueryAsync(ct);
        }

        // ISO-8601 round-trip strings in UTC sort and compare correctly as text.
        private static string Iso(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        private static DateTime ParseIso(string value) =>
            DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        private static object Db(string value) => value ?? (object)DBNull.Value;

        private static CleanupRecord MapRecord(IDataReader reader)
        {
            return new CleanupRecord
            {
                Id = reader.GetString(0),
                Scope = reader.GetString(1),
                TypeName = reader.GetString(2),
                MethodKey = reader.GetString(3),
                Signature = reader.GetString(4),
                ArgumentsJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                CreatedAt = ParseIso(reader.GetString(7)),
                Status = (CleanupStatus)reader.GetInt32(8),
                LastAttemptAt = reader.IsDBNull(9) ? null : (DateTime?)ParseIso(reader.GetString(9)),
                AttemptCount = reader.GetInt32(10),
                MaxRetries = reader.GetInt32(11),
                ErrorMessage = reader.IsDBNull(12) ? null : reader.GetString(12),
                Order = reader.GetInt32(13)
            };
        }
    }
}
