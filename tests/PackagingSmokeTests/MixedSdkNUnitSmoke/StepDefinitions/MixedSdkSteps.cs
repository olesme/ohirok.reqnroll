using Reqnroll;

namespace MixedSdkNUnitSmoke.StepDefinitions
{
    [Binding]
    public sealed class MixedSdkSteps(ScenarioContext sc)
    {
        [Then("the browser is one of the variants")]
        public void BrowserIsAVariant()
        {
            var browser = sc.ScenarioInfo.Arguments["Browser"] as string;
            if (browser is not ("Chrome" or "Firefox"))
                throw new System.Exception($"Expected the Browser variant to be Chrome or Firefox, got '{browser}'");
        }
    }
}
