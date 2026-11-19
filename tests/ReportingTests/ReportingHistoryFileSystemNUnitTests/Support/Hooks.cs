namespace ReportingHistoryFileSystemNUnitTests.Support
{
    [Binding]
    public static class Hooks
    {
        [AfterScenario]
        public static Task AfterScenario()
        {
            var filePath = "test-reporting-history.ndjson";
            if (File.Exists(filePath))
                File.Delete(filePath);

            return Task.CompletedTask;
        }
    }
}
