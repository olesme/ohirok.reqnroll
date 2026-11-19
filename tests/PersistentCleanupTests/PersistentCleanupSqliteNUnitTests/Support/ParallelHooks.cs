using Microsoft.Data.Sqlite;
using PersistentCleanupSqliteNUnitTests.Bindings;

namespace PersistentCleanupSqliteNUnitTests.Support
{
    [Binding]
    public static class ParallelHooks
    {
        // Static lock for database initialization (one-time setup)
        private static readonly object DbInitLock = new object();
        private static bool _dbInitialized = false;

        [BeforeTestRun(Order = 1)]
        public static async Task BeforeTestRun()
        {
            lock (DbInitLock)
            {
                if (!_dbInitialized)
                {
                    // Ensure database and tables exist
                    var dbPath = "parallel-test-cleanup.db";
                    using var connection = new SqliteConnection($"Data Source={dbPath}");
                    connection.Open();

                    var createTableSql = @"
                        CREATE TABLE IF NOT EXISTS CleanupRecords (
                            Id INTEGER PRIMARY KEY AUTOINCREMENT,
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

                        CREATE INDEX IF NOT EXISTS IX_CleanupRecords_Status 
                            ON CleanupRecords(Status);
                        
                        CREATE INDEX IF NOT EXISTS IX_CleanupRecords_MethodKey 
                            ON CleanupRecords(MethodKey);
                        
                        CREATE INDEX IF NOT EXISTS IX_CleanupRecords_Order 
                            ON CleanupRecords([Order], CreatedAt);
                    ";

                    new SqliteCommand(createTableSql, connection).ExecuteNonQuery();
                    _dbInitialized = true;
                }
            }
        }

        [BeforeScenario(Order = 1)]
        public static void BeforeScenario(ScenarioContext scenarioContext)
        {
            TestCleanups.Reset();

            // Store scenario start time for potential cleanup of old records
            scenarioContext["StartTime"] = DateTime.UtcNow;
        }

        [AfterScenario(Order = 999)]
        public static void AfterScenario(ScenarioContext scenarioContext)
        {
            TestCleanups.Reset();

            // Note: We do NOT clear the database here due to parallel execution
            // Instead, we rely on scenario-specific identifiers for isolation
            // Old test data can be cleaned up with a maintenance task if needed
        }

        [AfterTestRun(Order = 999)]
        public static async Task AfterTestRun()
        {
            // Optional: Clean up very old test records (e.g., > 1 hour old)
            // This prevents the database from growing indefinitely in development
            var dbPath = "parallel-test-cleanup.db";

            try
            {
                using var connection = new SqliteConnection($"Data Source={dbPath}");
                await connection.OpenAsync();

                var cutoffTime = DateTime.UtcNow.AddHours(-1).ToString("O");
                var cleanupSql = @"
                    DELETE FROM CleanupRecords 
                    WHERE CreatedAt < @Cutoff
                ";

                using var command = new SqliteCommand(cleanupSql, connection);
                command.Parameters.AddWithValue("@Cutoff", cutoffTime);

                var deleted = await command.ExecuteNonQueryAsync();
                if (deleted > 0)
                {
                    Console.WriteLine($"Cleaned up {deleted} old test records");
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
