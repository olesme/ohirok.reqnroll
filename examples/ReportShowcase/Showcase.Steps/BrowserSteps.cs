using Reqnroll;

namespace Showcase.Steps;

/// <summary>Steps of the cross-browser feature (Ohirok.Variants): the browser comes from the variant, as an outline argument.</summary>
[Binding]
public sealed class BrowserSteps(ScenarioContext scenarioContext)
{
    [Given("the storefront is opened in {word}")]
    public void StorefrontIsOpened(string browser) => AssertVariant(browser);

    [Then("the page renders in {word}")]
    public void PageRenders(string browser) => AssertVariant(browser);

    [Then("the cart badge shows {int} in {word}")]
    public void CartBadgeShows(int items, string browser)
    {
        AssertVariant(browser);
        if (items <= 0) throw new InvalidOperationException($"The cart badge cannot show {items} items.");
    }

    private void AssertVariant(string browser)
    {
        var variant = scenarioContext.ScenarioInfo.Arguments["Browser"] as string;
        if (browser != variant) throw new InvalidOperationException($"The step says {browser}, the variant is {variant}.");
    }
}
