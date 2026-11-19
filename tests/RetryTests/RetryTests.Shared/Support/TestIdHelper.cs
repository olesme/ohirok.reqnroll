namespace RetryTests.Support
{
    internal static class TestIdHelper
    {
        public static string GetScenarioId(ScenarioContext sc)
        {
            var featureContext = sc.ScenarioContainer.Resolve<FeatureContext>();
            var args = sc.ScenarioInfo.Arguments.Values.Cast<object>();
            var suffix = string.Join("|", args);
            return $"{featureContext.FeatureInfo.Title}||{sc.ScenarioInfo.Title}||{suffix}";
        }
    }
}
