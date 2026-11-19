using Microsoft.Data.Sqlite;
using Ohirok.PersistentCleanup;

namespace PersistentCleanupSqliteNUnitTests.Support
{
    /// <summary>
    /// Direct access to the SQLite file configured in reqnroll.json ("dbPath"), for assertions that look at what
    /// is really persisted, and for simulating a removed cleanup method.
    /// </summary>
    public static class SqliteTestDb
    {
        // Must be the same value as persistentCleanup.dbPath in reqnroll.json.
        public const string DbPath = "test-cleanup.db";

        private static string ConnectionString => $"Data Source={Path.GetFullPath(DbPath)}";

        /// <summary>Status of the record, or null if there is no such record (e.g. a completed one was deleted).</summary>
        public static CleanupStatus? ReadStatus(string id)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = new SqliteCommand("SELECT Status FROM CleanupRecords WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", id);

            var value = command.ExecuteScalar();
            return value == null || value is DBNull ? null : (CleanupStatus)Convert.ToInt32(value);
        }

        /// <summary>Renames the cleanup method key of a persisted record: the method then "no longer exists in code".</summary>
        public static void SetMethodKey(string id, string methodKey)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            using var command = new SqliteCommand("UPDATE CleanupRecords SET MethodKey = @Key WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Key", methodKey);
            command.Parameters.AddWithValue("@Id", id);
            command.ExecuteNonQuery();
        }

        public static void DeleteAllRecords()
        {
            try
            {
                using var connection = new SqliteConnection(ConnectionString);
                connection.Open();
                using var command = new SqliteCommand("DELETE FROM CleanupRecords", connection);
                command.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // The table is created on first use of the cleanup service; nothing to delete before that.
            }
        }

        public static void DeleteDatabaseFile()
        {
            foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
            {
                try { File.Delete(Path.GetFullPath(DbPath + suffix)); }
                catch (IOException) { }
            }
        }
    }
}
