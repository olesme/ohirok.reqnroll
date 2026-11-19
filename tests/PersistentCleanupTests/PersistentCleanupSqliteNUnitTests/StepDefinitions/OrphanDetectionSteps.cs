using PersistentCleanupSqliteNUnitTests.Support;

namespace PersistentCleanupSqliteNUnitTests.StepDefinitions
{
    [Binding]
    public class OrphanDetectionSteps(ScenarioContext scenarioContext)
    {
        // A removed or renamed [Cleanup] method cannot be simulated by unloading an assembly, so the persisted
        // record is edited instead: its method key then points to a method that does not exist.
        [When("I simulate method removal for {string}")]
        public void WhenSimulateMethodRemoval(string resourceId)
        {
            var id = (string)scenarioContext[$"cleanupId_{resourceId}"];
            SqliteTestDb.SetMethodKey(id, "MethodThatWasRemoved");
        }
    }
}
