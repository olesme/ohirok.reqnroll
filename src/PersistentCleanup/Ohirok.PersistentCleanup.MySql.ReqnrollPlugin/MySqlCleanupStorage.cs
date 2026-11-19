using MySqlConnector;

namespace Ohirok.PersistentCleanup.MySql.ReqnrollPlugin
{
    public sealed class MySqlCleanupStorage : ICleanupStorage
    {
        private const string Columns =
            "Id, Scope, TypeName, MethodKey, Signature, ArgumentsJson, Description, CreatedAt, Status, " +
            "LastAttemptAt, AttemptCount, MaxRetries, ErrorMessage, `Order`";

        private readonly string _connectionString;
        private readonly bool _autoCreate;

        public MySqlCleanupStorage(string connectionString, bool autoCreate = true)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be empty", nameof(connectionString));

            _connectionString = connectionString;
            _autoCreate = autoCreate;
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            if (!_autoCreate)
                return;

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                CREATE TABLE IF NOT EXISTS CleanupRecords (
                    Id VARCHAR(32) NOT NULL PRIMARY KEY,
                    Scope VARCHAR(200) NOT NULL DEFAULT '',
                    TypeName VARCHAR(500) NOT NULL,
                    MethodKey VARCHAR(200) NOT NULL,
                    Signature VARCHAR(1000) NOT NULL,
                    ArgumentsJson LONGTEXT,
                    Description TEXT,
                    CreatedAt DATETIME(6) NOT NULL,
                    Status INT NOT NULL,
                    LastAttemptAt DATETIME(6),
                    AttemptCount INT NOT NULL DEFAULT 0,
                    MaxRetries INT NOT NULL DEFAULT 3,
                    ErrorMessage TEXT,
                    `Order` INT NOT NULL DEFAULT 10000,
                    INDEX IX_CleanupRecords_Status_Scope (Status, Scope),
                    INDEX IX_CleanupRecords_Order (`Order`, CreatedAt)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ";

            using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = $@"
                INSERT INTO CleanupRecords ({Columns})
                VALUES (@Id, @Scope, @TypeName, @MethodKey, @Signature, @ArgumentsJson, @Description,
                        @CreatedAt, @Status, @LastAttemptAt, @AttemptCount, @MaxRetries, @ErrorMessage, @Order)";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", record.Id);
            command.Parameters.AddWithValue("@Scope", record.Scope ?? string.Empty);
            command.Parameters.AddWithValue("@TypeName", record.TypeName);
            command.Parameters.AddWithValue("@MethodKey", record.MethodKey);
            command.Parameters.AddWithValue("@Signature", record.Signature);
            command.Parameters.AddWithValue("@ArgumentsJson", Db(record.ArgumentsJson));
            command.Parameters.AddWithValue("@Description", Db(record.Description));
            command.Parameters.AddWithValue("@CreatedAt", record.CreatedAt);
            command.Parameters.AddWithValue("@Status", (int)record.Status);
            command.Parameters.AddWithValue("@LastAttemptAt", Db(record.LastAttemptAt));
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

            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var statusList = string.Join(",", statuses.Select(s => (int)s)); // integers only
            var sql = $"SELECT {Columns} FROM CleanupRecords WHERE Status IN ({statusList})";

            using var command = new MySqlCommand { Connection = connection };
            if (scope != null)
            {
                sql += " AND Scope = @Scope";
                command.Parameters.AddWithValue("@Scope", scope);
            }

            command.CommandText = sql + " ORDER BY `Order`, CreatedAt";
            return await ReadRecordsAsync(command, ct);
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new MySqlCommand($"SELECT {Columns} FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);

            return (await ReadRecordsAsync(command, ct)).FirstOrDefault();
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // One UPDATE is atomic: of several competing processes exactly one changes the row.
            var sql = @"
                UPDATE CleanupRecords
                SET Status = @Processing, LastAttemptAt = @Now
                WHERE Id = @Id AND Status = @Expected";

            using var command = new MySqlCommand { Connection = connection };
            command.Parameters.AddWithValue("@Processing", (int)CleanupStatus.Processing);
            command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Expected", (int)expectedStatus);

            if (lastAttemptBefore.HasValue)
            {
                sql += " AND (LastAttemptAt IS NULL OR LastAttemptAt < @Before)";
                command.Parameters.AddWithValue("@Before", lastAttemptBefore.Value);
            }

            command.CommandText = sql;
            return await command.ExecuteNonQueryAsync(ct) > 0;
        }

        public async Task UpdateStatusAsync(
            string id,
            CleanupStatus status,
            string errorMessage = null,
            int? attemptCount = null,
            CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                UPDATE CleanupRecords
                SET Status = @Status,
                    LastAttemptAt = @Now,
                    ErrorMessage = @ErrorMessage,
                    AttemptCount = COALESCE(@AttemptCount, AttemptCount)
                WHERE Id = @Id";

            using var command = new MySqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", id);
            command.Parameters.AddWithValue("@Status", (int)status);
            command.Parameters.AddWithValue("@Now", DateTime.UtcNow);
            command.Parameters.AddWithValue("@ErrorMessage", Db(errorMessage));
            command.Parameters.AddWithValue("@AttemptCount", Db(attemptCount));

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new MySqlCommand("DELETE FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var conditions = new List<string>();
            using var command = new MySqlCommand { Connection = connection };

            if (olderThan.HasValue)
            {
                conditions.Add("CreatedAt < @OlderThan");
                command.Parameters.AddWithValue("@OlderThan", olderThan.Value);
            }

            if (status.HasValue)
            {
                conditions.Add("Status = @Status");
                command.Parameters.AddWithValue("@Status", (int)status.Value);
            }

            if (scope != null)
            {
                conditions.Add("Scope = @Scope");
                command.Parameters.AddWithValue("@Scope", scope);
            }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            command.CommandText = $"DELETE FROM CleanupRecords {where}";

            return await command.ExecuteNonQueryAsync(ct);
        }

        private static object Db(object value) => value ?? DBNull.Value;

        // DATETIME(6) comes back with Kind=Unspecified; everything stored here is UTC.
        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static async Task<List<CleanupRecord>> ReadRecordsAsync(MySqlCommand command, CancellationToken ct)
        {
            var records = new List<CleanupRecord>();

            using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                records.Add(new CleanupRecord
                {
                    Id = reader.GetString(0),
                    Scope = reader.GetString(1),
                    TypeName = reader.GetString(2),
                    MethodKey = reader.GetString(3),
                    Signature = reader.GetString(4),
                    ArgumentsJson = reader.IsDBNull(5) ? null : reader.GetString(5),
                    Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = Utc(reader.GetDateTime(7)),
                    Status = (CleanupStatus)reader.GetInt32(8),
                    LastAttemptAt = reader.IsDBNull(9) ? null : (DateTime?)Utc(reader.GetDateTime(9)),
                    AttemptCount = reader.GetInt32(10),
                    MaxRetries = reader.GetInt32(11),
                    ErrorMessage = reader.IsDBNull(12) ? null : reader.GetString(12),
                    Order = reader.GetInt32(13)
                });
            }

            return records;
        }
    }
}
