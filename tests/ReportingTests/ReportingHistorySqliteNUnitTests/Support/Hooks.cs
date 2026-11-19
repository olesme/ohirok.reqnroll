using Microsoft.Data.Sqlite;

namespace ReportingHistorySqliteNUnitTests.Support
{
    [Binding]
    public static class Hooks
    {
        [AfterScenario]
        public static async Task AfterScenario()
        {
            var dbPath = "test-reporting-history.db";
            using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();
            await new SqliteCommand("DELETE FROM ReportHistoryRuns", connection).ExecuteNonQueryAsync();
        }
    }
}
