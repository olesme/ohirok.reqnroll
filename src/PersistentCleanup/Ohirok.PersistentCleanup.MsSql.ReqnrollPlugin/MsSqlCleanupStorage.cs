using System.Data;
using Microsoft.Data.SqlClient;

namespace Ohirok.PersistentCleanup.MsSql.ReqnrollPlugin
{
    public sealed class MsSqlCleanupStorage : ICleanupStorage
    {
        private const string Columns =
            "Id, Scope, TypeName, MethodKey, Signature, ArgumentsJson, Description, CreatedAt, Status, " +
            "LastAttemptAt, AttemptCount, MaxRetries, ErrorMessage, [Order]";

        private readonly string _connectionString;
        private readonly bool _autoCreate;

        public MsSqlCleanupStorage(string connectionString, bool autoCreate = true)
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

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CleanupRecords')
                BEGIN
                    CREATE TABLE CleanupRecords (
                        Id NVARCHAR(32) NOT NULL PRIMARY KEY,
                        Scope NVARCHAR(200) NOT NULL DEFAULT '',
                        TypeName NVARCHAR(500) NOT NULL,
                        MethodKey NVARCHAR(200) NOT NULL,
                        Signature NVARCHAR(1000) NOT NULL,
                        ArgumentsJson NVARCHAR(MAX),
                        Description NVARCHAR(MAX),
                        CreatedAt DATETIME2 NOT NULL,
                        Status INT NOT NULL,
                        LastAttemptAt DATETIME2,
                        AttemptCount INT NOT NULL DEFAULT 0,
                        MaxRetries INT NOT NULL DEFAULT 3,
                        ErrorMessage NVARCHAR(MAX),
                        [Order] INT NOT NULL DEFAULT 10000
                    );

                    CREATE INDEX IX_CleanupRecords_Status_Scope ON CleanupRecords(Status, Scope);
                    CREATE INDEX IX_CleanupRecords_Order ON CleanupRecords([Order], CreatedAt);
                END
            ";

            using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task SaveAsync(CleanupRecord record, CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var sql = $@"
                INSERT INTO CleanupRecords ({Columns})
                VALUES (@Id, @Scope, @TypeName, @MethodKey, @Signature, @ArgumentsJson, @Description,
                        @CreatedAt, @Status, @LastAttemptAt, @AttemptCount, @MaxRetries, @ErrorMessage, @Order)";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 32).Value = record.Id;
            command.Parameters.Add("@Scope", SqlDbType.NVarChar, 200).Value = record.Scope ?? string.Empty;
            command.Parameters.Add("@TypeName", SqlDbType.NVarChar, 500).Value = record.TypeName;
            command.Parameters.Add("@MethodKey", SqlDbType.NVarChar, 200).Value = record.MethodKey;
            command.Parameters.Add("@Signature", SqlDbType.NVarChar, 1000).Value = record.Signature;
            command.Parameters.Add("@ArgumentsJson", SqlDbType.NVarChar, -1).Value = Db(record.ArgumentsJson);
            command.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = Db(record.Description);
            command.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = record.CreatedAt;
            command.Parameters.Add("@Status", SqlDbType.Int).Value = (int)record.Status;
            command.Parameters.Add("@LastAttemptAt", SqlDbType.DateTime2).Value = Db(record.LastAttemptAt);
            command.Parameters.Add("@AttemptCount", SqlDbType.Int).Value = record.AttemptCount;
            command.Parameters.Add("@MaxRetries", SqlDbType.Int).Value = record.MaxRetries;
            command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, -1).Value = Db(record.ErrorMessage);
            command.Parameters.Add("@Order", SqlDbType.Int).Value = record.Order;

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<List<CleanupRecord>> GetByStatusesAsync(
            IReadOnlyCollection<CleanupStatus> statuses,
            string scope = null,
            CancellationToken ct = default)
        {
            if (statuses == null || statuses.Count == 0)
                return new List<CleanupRecord>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var statusList = string.Join(",", statuses.Select(s => (int)s)); // integers only
            var sql = $"SELECT {Columns} FROM CleanupRecords WHERE Status IN ({statusList})";

            using var command = new SqlCommand { Connection = connection };
            if (scope != null)
            {
                sql += " AND Scope = @Scope";
                command.Parameters.Add("@Scope", SqlDbType.NVarChar, 200).Value = scope;
            }

            command.CommandText = sql + " ORDER BY [Order], CreatedAt";
            return await ReadRecordsAsync(command, ct);
        }

        public async Task<CleanupRecord> GetByIdAsync(string id, CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new SqlCommand($"SELECT {Columns} FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 32).Value = id;

            return (await ReadRecordsAsync(command, ct)).FirstOrDefault();
        }

        public async Task<bool> TryClaimAsync(
            string id,
            CleanupStatus expectedStatus,
            DateTime? lastAttemptBefore,
            CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            // One UPDATE is atomic: of several competing processes exactly one changes the row.
            var sql = @"
                UPDATE CleanupRecords
                SET Status = @Processing, LastAttemptAt = @Now
                WHERE Id = @Id AND Status = @Expected";

            using var command = new SqlCommand { Connection = connection };
            command.Parameters.Add("@Processing", SqlDbType.Int).Value = (int)CleanupStatus.Processing;
            command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = DateTime.UtcNow;
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 32).Value = id;
            command.Parameters.Add("@Expected", SqlDbType.Int).Value = (int)expectedStatus;

            if (lastAttemptBefore.HasValue)
            {
                sql += " AND (LastAttemptAt IS NULL OR LastAttemptAt < @Before)";
                command.Parameters.Add("@Before", SqlDbType.DateTime2).Value = lastAttemptBefore.Value;
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
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            const string sql = @"
                UPDATE CleanupRecords
                SET Status = @Status,
                    LastAttemptAt = @Now,
                    ErrorMessage = @ErrorMessage,
                    AttemptCount = COALESCE(@AttemptCount, AttemptCount)
                WHERE Id = @Id";

            using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 32).Value = id;
            command.Parameters.Add("@Status", SqlDbType.Int).Value = (int)status;
            command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = DateTime.UtcNow;
            command.Parameters.Add("@ErrorMessage", SqlDbType.NVarChar, -1).Value = Db(errorMessage);
            command.Parameters.Add("@AttemptCount", SqlDbType.Int).Value = Db(attemptCount);

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteAsync(string id, CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            using var command = new SqlCommand("DELETE FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.Add("@Id", SqlDbType.NVarChar, 32).Value = id;

            await command.ExecuteNonQueryAsync(ct);
        }

        public async Task<int> DeleteWhereAsync(
            DateTime? olderThan,
            CleanupStatus? status,
            string scope,
            CancellationToken ct = default)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var conditions = new List<string>();
            using var command = new SqlCommand { Connection = connection };

            if (olderThan.HasValue)
            {
                conditions.Add("CreatedAt < @OlderThan");
                command.Parameters.Add("@OlderThan", SqlDbType.DateTime2).Value = olderThan.Value;
            }

            if (status.HasValue)
            {
                conditions.Add("Status = @Status");
                command.Parameters.Add("@Status", SqlDbType.Int).Value = (int)status.Value;
            }

            if (scope != null)
            {
                conditions.Add("Scope = @Scope");
                command.Parameters.Add("@Scope", SqlDbType.NVarChar, 200).Value = scope;
            }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            command.CommandText = $"DELETE FROM CleanupRecords {where}";

            return await command.ExecuteNonQueryAsync(ct);
        }

        private static object Db(object value) => value ?? DBNull.Value;

        // DATETIME2 comes back with Kind=Unspecified; everything stored here is UTC.
        private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

        private static async Task<List<CleanupRecord>> ReadRecordsAsync(SqlCommand command, CancellationToken ct)
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
