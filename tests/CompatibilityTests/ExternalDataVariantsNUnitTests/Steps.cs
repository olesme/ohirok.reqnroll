using Reqnroll;

namespace ExternalDataVariantsNUnitTests;

[Binding]
public class Steps
{
    [Given("the product {word} in {word}")]
    public void ProductInBrowser(string product, string browser)
    {
        if (product is not ("Apple" or "Pear") || browser is not ("Chrome" or "Firefox"))
            throw new InvalidOperationException($"Unexpected combination: {product} in {browser}");
    }
}
