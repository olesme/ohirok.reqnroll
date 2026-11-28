using Reqnroll;

namespace AutoTagsTests.StepDefinitions;

[Binding]
public class AutoTagSteps(ScenarioContext scenarioContext)
{
    private const string MockServerHookKey = "mock server hook";

    // The reason a scenario needs the tag: preparation scoped by it.
    [BeforeScenario("MockServer")]
    public void StartMockServer() => scenarioContext[MockServerHookKey] = true;

    [Given(@"(?i)the mock server (?:returns|responds) (.*)")]
    public void MockServerStep(string _) { }

    [Given("an order exists in the database")]
    public void OrderInDatabase() { }

    [When("I add the product to the cart")]
    public void AddToCart() { }

    [Then(@"the scenario is tagged (\S+)")]
    public void IsTagged(string tag)
    {
        if (!scenarioContext.ScenarioInfo.CombinedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected tag '{tag}', got: {string.Join(", ", scenarioContext.ScenarioInfo.CombinedTags)}.");
    }

    [Then(@"the scenario is not tagged (\S+)")]
    public void IsNotTagged(string tag)
    {
        if (scenarioContext.ScenarioInfo.CombinedTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unexpected tag '{tag}'.");
    }

    [Then("the mock server hook ran")]
    public void HookRan()
    {
        if (!scenarioContext.ContainsKey(MockServerHookKey))
            throw new InvalidOperationException("[BeforeScenario(\"MockServer\")] did not run.");
    }

    [Then("the mock server hook did not run")]
    public void HookDidNotRun()
    {
        if (scenarioContext.ContainsKey(MockServerHookKey))
            throw new InvalidOperationException("[BeforeScenario(\"MockServer\")] ran.");
    }
}
