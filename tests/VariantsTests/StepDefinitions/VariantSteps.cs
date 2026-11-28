using Reqnroll;

namespace VariantsTests.StepDefinitions;

[Binding]
public class VariantSteps(ScenarioContext scenarioContext)
{
    private bool _backgroundRan;

    [Given("the background ran")]
    public void BackgroundRan() => _backgroundRan = true;

    [Then(@"the variant (\w+) is one of (.+)")]
    public void VariantIsOneOf(string key, string values)
    {
        var allowed = values.Split(',').Select(v => v.Trim()).ToArray();
        var actual = Variant(key) ?? throw new InvalidOperationException($"Variant '{key}' is not set.");
        if (!allowed.Contains(actual))
            throw new InvalidOperationException($"Variant '{key}' is '{actual}', expected one of {string.Join(", ", allowed)}.");
    }

    [Then(@"the variant (\w+) is not set")]
    public void VariantIsNotSet(string key)
    {
        if (Variant(key) is { } actual) throw new InvalidOperationException($"Variant '{key}' is '{actual}', expected none.");
    }

    [Then(@"the step argument ""(.*)"" is the variant (\w+)")]
    public void StepArgumentIsVariant(string argument, string key)
    {
        if (argument != Variant(key))
            throw new InvalidOperationException($"Step argument '{argument}' differs from variant '{key}' = '{Variant(key)}'.");
    }

    [AfterScenario]
    public void BackgroundRanForEveryVariant()
    {
        // Only the scenario-level feature has a Background.
        if (scenarioContext.ScenarioInfo.Title.Length > 0 && FeatureHasBackground() && !_backgroundRan)
            throw new InvalidOperationException("The Background did not run for this variant.");
    }

    private bool FeatureHasBackground()
        => scenarioContext.ScenarioInfo.Title is { } _ && scenarioContext.ScenarioContainer.Resolve<FeatureContext>().FeatureInfo.Title == "Scenario-level variants";

    // Variants are Examples columns, so Reqnroll exposes them like any outline argument.
    private string? Variant(string key)
    {
        foreach (System.Collections.DictionaryEntry entry in scenarioContext.ScenarioInfo.Arguments)
            if (string.Equals((string)entry.Key, key, StringComparison.OrdinalIgnoreCase)) return (string?)entry.Value;
        return null;
    }
}
