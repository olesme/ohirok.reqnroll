namespace XFailNUnitTests.Support
{
    [Binding]
    public static class XFailHooks
    {
        [BeforeScenario]
        public static void BeforeScenario(ScenarioContext scenarioContext)
        {
            scenarioContext["XFailExceptionCaught"] = false;
        }
    }
}
