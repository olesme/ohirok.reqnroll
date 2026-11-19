using PersistentCleanupSqliteNUnitTests.Bindings;

namespace PersistentCleanupSqliteNUnitTests.Support
{
    [Binding]
    public static class Hooks
    {
        // Start from an empty database. The file is created when the cleanup service is first used,
        // which happens after this hook (autoPerformOnStart is off in reqnroll.json).
        [BeforeTestRun(Order = 0)]
        public static void BeforeTestRun() => SqliteTestDb.DeleteDatabaseFile();

        [BeforeScenario(Order = 1)]
        public static void BeforeScenario(ScenarioContext scenarioContext)
        {
            // @parallel scenarios use unique identifiers and must not wipe state of scenarios that are still running.
            if (!IsParallel(scenarioContext))
                TestCleanups.Reset();
        }

        [AfterScenario(Order = 999)]
        public static void AfterScenario(ScenarioContext scenarioContext)
        {
            if (IsParallel(scenarioContext))
                return;

            TestCleanups.Reset();
            SqliteTestDb.DeleteAllRecords();
        }

        private static bool IsParallel(ScenarioContext scenarioContext) =>
            scenarioContext.ScenarioInfo.Tags.Contains("parallel");
    }
}
