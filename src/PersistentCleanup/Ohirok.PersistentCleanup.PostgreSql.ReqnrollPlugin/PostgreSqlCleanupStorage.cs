using Npgsql;
using NpgsqlTypes;

namespace Ohirok.PersistentCleanup.PostgreSql.ReqnrollPlugin
{
    public sealed class PostgreSqlCleanupStorage : ICleanupStorage
    {
        // Timestamps are TIMESTAMPTZ: Npgsql 6+ refuses to write a UTC DateTime into a plain TIMESTAMP column.
        private const string Columns =
            "id, scope, type_name, method_key, signature, arguments_json, description, created_at, status, " +
            "last_attempt_at, attempt_count, max_retries, error_message, \"order\"";

        private readonly string _connectionString;
        private readonly bool _autoCreate;

        public PostgreSqlCleanupStorage(string connectionString, bool autoCreate = true)
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

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                CREATE TABLE IF NOT EXISTS cleanup_records (
                    id VARCHAR(32) PRIMARY KEY,
                    scope VARCHAR(200) NOT NULL DEFAULT '',
                    type_name VARCHAR(500) NOT NULL,
                    method_key VARCHAR(200) NOT NULL,
                    signature VARCHAR(1000) NOT NULL,
                    arguments_json TEXT,
                    description TEXT,
                    created_at TIMESTAMPTZ NOT NULL,
                    status INTEGER NOT NULL,
                    last_attempt_at TIMESTAMPTZ,
                    attempt_count INTEGER NOT NULL DEFAULT 0,
                    max_retries INTEGER NOT NULL DEFAULT 3,
                    error_message TEXT,
                    ""order"" INTEGER NOT NULL DEFAULT 10000
                );

                CREATE INDEX IF NOT EXISTS ix_cleanup_records_status_scope ON cleanup_records(status, scope);
                CREATE INDEX IF NOT EXISTS ix_cleanup_records_order ON cleanup_records(""order"", created_at);
            ";

            using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = $@"
                INSERT INTO cleanup_records ({Columns})
                VALUES (@Id, @Scope, @TypeName, @MethodKey, @Signature, @ArgumentsJson, @Description,
                        @CreatedAt, @Status, @LastAttemptAt, @AttemptCount, @MaxRetries, @ErrorMessage, @Order)";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add(Text("@Id", record.Id));
            command.Parameters.Add(Text("@Scope", record.Scope ?? string.Empty));
            command.Parameters.Add(Text("@TypeName", record.TypeName));
            command.Parameters.Add(Text("@MethodKey", record.MethodKey));
            command.Parameters.Add(Text("@Signature", record.Signature));
            command.Parameters.Add(Text("@ArgumentsJson", record.ArgumentsJson));
            command.Parameters.Add(Text("@Description", record.Description));
            command.Parameters.Add(Time("@CreatedAt", record.CreatedAt));
            command.Parameters.Add(Int("@Status", (int)record.Status));
            command.Parameters.Add(Time("@LastAttemptAt", record.LastAttemptAt));
            command.Parameters.Add(Int("@AttemptCount", record.AttemptCount));
            command.Parameters.Add(Int("@MaxRetries", record.MaxRetries));
            command.Parameters.Add(Text("@ErrorMessage", record.ErrorMessage));
            command.Parameters.Add(Int("@Order", record.Order));

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default)
        {
            if (statuses == null || statuses.Count == 0)
                return new List<CleanupRecord>();

            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var statusList = string.Join(",", statuses.Select(s => (int)s)); // integers only
            var sql = $"SELECT {Columns} FROM cleanup_records WHERE status IN ({statusList})";

            using var command = new NpgsqlCommand { Connection = connection };
            if (scope != null)
            {
                sql += " AND scope = @Scope";
                command.Parameters.Add(Text("@Scope", scope));
            }

            command.CommandText = sql + " ORDER BY \"order\", created_at";
            return await ReadRecordsAsync(command, ct);
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new NpgsqlCommand($"SELECT {Columns} FROM cleanup_records WHERE id = @Id", connection);
            command.Parameters.Add(Text("@Id", id));

            return (await ReadRecordsAsync(command, ct)).FirstOrDefault();
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // One UPDATE is atomic: of several competing processes exactly one changes the row.
            var sql = @"
                UPDATE cleanup_records
                SET status = @Processing, last_attempt_at = @Now
                WHERE id = @Id AND status = @Expected";

            using var command = new NpgsqlCommand { Connection = connection };
            command.Parameters.Add(Int("@Processing", (int)CleanupStatus.Processing));
            command.Parameters.Add(Time("@Now", DateTime.UtcNow));
            command.Parameters.Add(Text("@Id", id));
            command.Parameters.Add(Int("@Expected", (int)expectedStatus));

            if (lastAttemptBefore.HasValue)
            {
                sql += " AND (last_attempt_at IS NULL OR last_attempt_at < @Before)";
                command.Parameters.Add(Time("@Before", lastAttemptBefore.Value));
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
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                UPDATE cleanup_records
                SET status = @Status,
                    last_attempt_at = @Now,
                    error_message = @ErrorMessage,
                    attempt_count = COALESCE(@AttemptCount, attempt_count)
                WHERE id = @Id";

            using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.Add(Text("@Id", id));
            command.Parameters.Add(Int("@Status", (int)status));
            command.Parameters.Add(Time("@Now", DateTime.UtcNow));
            command.Parameters.Add(Text("@ErrorMessage", errorMessage));
            command.Parameters.Add(Int("@AttemptCount", attemptCount));

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new NpgsqlCommand("DELETE FROM cleanup_records WHERE id = @Id", connection);
            command.Parameters.Add(Text("@Id", id));

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var conditions = new List<string>();
            using var command = new NpgsqlCommand { Connection = connection };

            if (olderThan.HasValue)
            {
                conditions.Add("created_at < @OlderThan");
                command.Parameters.Add(Time("@OlderThan", olderThan.Value));
            }

            if (status.HasValue)
            {
                conditions.Add("status = @Status");
                command.Parameters.Add(Int("@Status", (int)status.Value));
            }

            if (scope != null)
            {
                conditions.Add("scope = @Scope");
                command.Parameters.Add(Text("@Scope", scope));
            }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            command.CommandText = $"DELETE FROM cleanup_records {where}";

            return await command.ExecuteNonQueryAsync(ct);
        }

        // Explicitly typed parameters: Npgsql cannot infer the type of a NULL value.
        private static NpgsqlParameter Text(string name, string value) =>
            new NpgsqlParameter(name, NpgsqlDbType.Text) { Value = (object)value ?? DBNull.Value };

        private static NpgsqlParameter Int(string name, int? value) =>
            new NpgsqlParameter(name, NpgsqlDbType.Integer) { Value = value.HasValue ? (object)value.Value : DBNull.Value };

        private static NpgsqlParameter Time(string name, DateTime? value) =>
            new NpgsqlParameter(name, NpgsqlDbType.TimestampTz)
            {
                Value = value.HasValue ? (object)DateTime.SpecifyKind(value.Value.ToUniversalTime(), DateTimeKind.Utc) : DBNull.Value
            };

        private static async Task<List<CleanupRecord>> ReadRecordsAsync(NpgsqlCommand command, CancellationToken ct)
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
                    CreatedAt = reader.GetFieldValue<DateTime>(7),
                    Status = (CleanupStatus)reader.GetInt32(8),
                    LastAttemptAt = reader.IsDBNull(9) ? null : (DateTime?)reader.GetFieldValue<DateTime>(9),
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
