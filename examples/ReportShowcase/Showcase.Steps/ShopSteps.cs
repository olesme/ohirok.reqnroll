using Reqnroll;
using Shouldly;
using Showcase.Domain;

namespace Showcase.Steps;

/// <summary>Per-scenario state shared by every step class (Reqnroll creates one instance per scenario).</summary>
public sealed class ShopContext
{
    public Order Order { get; set; } = new();
    public CarrierRateApi Carrier { get; } = new();
    public Inventory Inventory { get; } = new();
    public IReadOnlyList<Product> SearchResults { get; set; } = [];
    public decimal? Quote { get; set; }
}

[Binding]
public sealed class CatalogSteps(ShopContext shop)
{
    [When("the customer searches for {string}")]
    public void Search(string text) => shop.SearchResults = Catalog.Search(text);

    [Then("{int} product(s) is/are found")]
    public void Found(int count) => shop.SearchResults.Count.ShouldBe(count);

    [Then("the results contain:")]
    public void ResultsContain(Table table)
    {
        foreach (var row in table.Rows)
            shop.SearchResults.Select(p => p.Sku).ShouldContain(row["sku"]);
    }

    [When("the customer types {string} into the search box")]
    public void Autocomplete(string prefix) =>
        shop.SearchResults = [.. SearchSuggestions.Suggest(prefix).Select(name => Catalog.Products.First(p => p.Name == name))];

    [Then("the suggestions include {string}")]
    public void SuggestionsInclude(string name) => shop.SearchResults.Select(p => p.Name).ShouldContain(name);

    [Then("the product page of {string} recommends other products only")]
    public void RecommendsOthers(string sku) => Recommendations.For(sku).ShouldNotContain(sku);
}

[Binding]
public sealed class OrderSteps(ShopContext shop)
{
    [Given("a {word} customer")]
    public void Customer(string kind) => shop.Order.Customer = kind switch
    {
        "guest" => CustomerKind.Guest,
        "returning-guest" => CustomerKind.ReturningGuest,
        "member" => CustomerKind.Member,
        _ => throw new ArgumentException($"Unknown customer kind '{kind}'."),
    };

    [Given("the customer ships to {word}")]
    public void ShipsTo(string country) => shop.Order.Country = country;

    [Given("the cart contains {int} x {word}")]
    [When("the customer adds {int} x {word}")]
    public void Add(int quantity, string sku) => shop.Order.Add(sku, quantity);

    [Given("the cart contains:")]
    public void CartContains(Table table)
    {
        foreach (var row in table.Rows)
            shop.Order.Add(row["sku"], int.Parse(row["quantity"]));
    }

    [Given("the customer asks for gift wrapping")]
    public void GiftWrap() => shop.Order.GiftWrap = true;

    [Given("the order note is:")]
    public void Note(string note) => shop.Order.Note = note;

    [Then("the subtotal is {decimal} EUR")]
    public void Subtotal(decimal expected) => shop.Order.Subtotal.ShouldBe(expected);

    [Then("the discount is {decimal} EUR")]
    public void Discount(decimal expected) => Pricing.Discount(shop.Order).ShouldBe(expected);

    [Then("the gift wrapping fee is {decimal} EUR")]
    public void GiftWrapFee(decimal expected) => Pricing.GiftWrapFee(shop.Order).ShouldBe(expected);

    [Then("the VAT on {decimal} EUR in {word} is {decimal} EUR")]
    public void Vat(decimal net, string country, decimal expected) => Pricing.Vat(net, country).ShouldBe(expected);

    [Then("the order note is kept verbatim")]
    public void NoteKept() => shop.Order.Note.ShouldNotBeNullOrWhiteSpace();

    [Then("the order weighs {decimal} kg")]
    public void Weight(decimal expected) => shop.Order.WeightKg.ShouldBe(expected);

    [When("the customer adds {int} x {word} with a negative quantity")]
    public void AddNegative(int quantity, string sku) => shop.Order.Add(sku, -quantity);

    [When("the customer adds an unknown product {word}")]
    public void AddUnknown(string sku) => shop.Order.Add(sku);
}

[Binding]
public sealed class ShippingSteps(ShopContext shop)
{
    [Then("shipping to zone {word} for {decimal} kg costs {decimal} EUR")]
    public void Cost(string zone, decimal kg, decimal expected) => Shipping.Cost(zone, kg).ShouldBe(expected);

    [Then("shipping is free")]
    public void Free() => Shipping.Cost(Shipping.ZoneOf(shop.Order.Country), shop.Order.WeightKg, shop.Order.Subtotal).ShouldBe(0m);

    [Then("shipping costs {decimal} EUR")]
    public void Costs(decimal expected) =>
        Shipping.Cost(Shipping.ZoneOf(shop.Order.Country), shop.Order.WeightKg, shop.Order.Subtotal).ShouldBe(expected);

    [When("the carrier is asked for a live quote")]
    public void LiveQuote() => shop.Quote = shop.Carrier.Quote(Shipping.ZoneOf(shop.Order.Country), shop.Order.WeightKg);

    [Then("the live quote is {decimal} EUR")]
    public void QuoteIs(decimal expected) => shop.Quote.ShouldBe(expected);
}

[Binding]
public sealed class InventorySteps(ShopContext shop)
{
    [When("the customer reserves {int} x {word}")]
    public void Reserve(int quantity, string sku) => shop.Inventory.Reserve(sku, quantity);

    [Then("{int} x {word} remain(s) in stock")]
    public void Remaining(int expected, string sku) => shop.Inventory.Available(sku).ShouldBe(expected);

    [Then("reserving {int} x {word} is refused")]
    public void Refused(int quantity, string sku) =>
        Should.Throw<InvalidOperationException>(() => shop.Inventory.Reserve(sku, quantity));
}

[Binding]
public sealed class MatrixSteps
{
    [Then("{decimal} rounded in {word} is {decimal}")]
    public void Rounded(decimal amount, string currency, decimal expected) => Money.Round(amount, currency).ShouldBe(expected);

    [Then("a {word} buying {int} units of {decimal} EUR gets {decimal} EUR off")]
    public void DiscountMatrix(string kind, int units, decimal price, decimal expected)
    {
        var order = new Order { Customer = kind == "member" ? CustomerKind.Member : CustomerKind.Guest };
        order.Lines.Add(new OrderLine(new Product("X", "x", price, "x", 0m), units));
        Pricing.Discount(order).ShouldBe(expected);
    }
}
