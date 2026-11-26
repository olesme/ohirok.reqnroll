using Ohirok.PluginChains.Common.Reporting;
using Ohirok.Retry.Attributes;
using Ohirok.Retry.Core;
using Reqnroll;
using Showcase.Domain;

namespace Showcase.Steps;

[Binding]
public sealed class Hooks(ScenarioContext scenario, IReqnrollOutputHelper output)
{
    [BeforeScenario(Order = 0)]
    public void Environment() => output.WriteLine($"Running against build {ShowcaseBuild.Number} of tea-shop.example");

    /// <summary>A teardown that fails: the report shows the failing After hook on a scenario whose steps all passed.</summary>
    [AfterScenario("teardown-bug")]
    public void CleanUpTestData() =>
        throw new IOException("Could not delete the test customer: the database connection was closed.");

    /// <summary>Retry hooks see every retry; here each one leaves a note in the report.</summary>
    [OnRetry]
    public void NoteRetry(RetryContext retry) =>
        output.EmbedWarning($"{retry.Type} retry {retry.Repeat} of {retry.MaxRetries} after: {retry.LastException?.Message}");
}

/// <summary>Two bindings for the same step text, so the step is ambiguous - one of the report's step statuses.</summary>
[Binding]
public sealed class AmbiguousSteps
{
    [When("the cart is emptied")]
    public void EmptyCart() { }

    [When(@"the cart is (.*)")]
    public void ChangeCart(string _) { }
}
