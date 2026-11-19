namespace XFailNUnitTests.Support
{
    [Binding]
    public sealed class ScenarioXFailHooks
    {
        [BeforeScenario("fails-in-before-hook")]
        public void FailBeforeTheFirstStep() =>
            throw new InvalidOperationException("Test data could not be prepared.");
    }
}
