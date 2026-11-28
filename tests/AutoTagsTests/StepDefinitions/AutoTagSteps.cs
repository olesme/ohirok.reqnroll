using Ohirok.StepHashtags.ReqnrollPlugin;
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

    private string[] _lastStepHashtags = [];
    private int _gatewayAttempts;

    [When("the payment gateway accepts the card")]
    public void GatewayAccepts() => _lastStepHashtags = scenarioContext.GetStepHashtags();

    // Retry runs the step again within the same scenario, so the counter survives the attempts.
    [When(@"the payment gateway fails (\d+) times")]
    public void GatewayFails(int times)
    {
        _lastStepHashtags = scenarioContext.GetStepHashtags();
        if (++_gatewayAttempts <= times)
            throw new InvalidOperationException($"Gateway failure {_gatewayAttempts}");
    }

    [Then(@"that step had the hashtag (\S+)")]
    public void StepHadHashtag(string hashtag)
    {
        if (!_lastStepHashtags.Contains(hashtag))
            throw new InvalidOperationException($"Expected hashtag '{hashtag}', the step had: {string.Join(", ", _lastStepHashtags)}.");
    }

    [Then("that step had no hashtags")]
    public void StepHadNoHashtags()
    {
        if (_lastStepHashtags.Length > 0)
            throw new InvalidOperationException($"Unexpected hashtags: {string.Join(", ", _lastStepHashtags)}.");
    }

    [When("I add the product to the cart")]
    public void AddToCartRecordingHashtags() => _lastStepHashtags = scenarioContext.GetStepHashtags();

    [Given("an order exists in the database")]
    public void OrderInDatabase() { }

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
