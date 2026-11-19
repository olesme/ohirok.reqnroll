namespace Showcase.Domain;

/// <summary>
/// Which CI build the showcase is simulating (environment variable OHIROK_SHOWCASE_BUILD, default 1). A few shop
/// services behave differently per build - the way a real dependency or a real regression would - so the run
/// history has flaky, regressed and fixed scenarios to show.
/// </summary>
public static class ShowcaseBuild
{
    public static int Number =>
        int.TryParse(Environment.GetEnvironmentVariable("OHIROK_SHOWCASE_BUILD"), out var n) && n > 0 ? n : 1;

    /// <summary>True while the pipeline reruns the failed scenarios of a build (OHIROK_SHOWCASE_RERUN=1).</summary>
    public static bool IsRerun => Environment.GetEnvironmentVariable("OHIROK_SHOWCASE_RERUN") == "1";
}

public sealed record Product(string Sku, string Name, decimal Price, string Category, decimal WeightKg);

public static class Catalog
{
    public static readonly IReadOnlyList<Product> Products =
    [
        new("TEA-001", "Sencha green tea", 12.50m, "Tea", 0.25m),
        new("TEA-002", "Earl Grey", 9.90m, "Tea", 0.25m),
        new("TEA-003", "Matcha ceremonial grade", 34.00m, "Tea", 0.10m),
        new("CUP-010", "Porcelain cup", 18.00m, "Tableware", 0.40m),
        new("CUP-011", "Glass teapot 1L", 42.00m, "Tableware", 1.10m),
        new("ACC-020", "Bamboo whisk", 15.00m, "Accessories", 0.05m),
        new("ACC-021", "Tea strainer", 6.50m, "Accessories", 0.05m),
        new("GFT-030", "Gift box: tea tasting set", 59.00m, "Gifts", 1.50m),
        new("GFT-031", "Gift card 50", 50.00m, "Gifts", 0.00m),
        new("KIT-040", "Cast-iron kettle", 89.00m, "Tableware", 2.80m),
    ];

    public static Product Find(string sku) =>
        Products.FirstOrDefault(p => p.Sku == sku) ?? throw new KeyNotFoundException($"No product with SKU '{sku}'.");

    public static IReadOnlyList<Product> Search(string text) =>
        [.. Products.Where(p => p.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                              || p.Category.Contains(text, StringComparison.OrdinalIgnoreCase))];
}

public sealed record OrderLine(Product Product, int Quantity)
{
    public decimal Total => Product.Price * Quantity;
}

public enum CustomerKind { Guest, ReturningGuest, Member }

public sealed class Order
{
    public List<OrderLine> Lines { get; } = [];
    public CustomerKind Customer { get; set; } = CustomerKind.Guest;
    public string Country { get; set; } = "DE";
    public string? Note { get; set; }
    public bool GiftWrap { get; set; }

    public int Units => Lines.Sum(l => l.Quantity);
    public decimal Subtotal => Lines.Sum(l => l.Total);
    public decimal WeightKg => Lines.Sum(l => l.Product.WeightKg * l.Quantity);

    public Order Add(string sku, int quantity = 1)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be positive.");
        Lines.Add(new OrderLine(Catalog.Find(sku), quantity));
        return this;
    }
}

public static class Pricing
{
    /// <summary>Members get 10%; guests pay the list price.</summary>
    public static decimal LoyaltyRate(CustomerKind customer) =>
        // Bug: a guest who once had an account is still treated as a member.
        customer is CustomerKind.Member or CustomerKind.ReturningGuest ? 0.10m : 0m;

    /// <summary>Bulk tiers by number of units: 10+ 5%, 50+ 10%, 100+ 15%.</summary>
    public static decimal BulkRate(int units) => units switch
    {
        >= 100 => 0.15m,
        > 50 => 0.10m,          // Bug: the 50-unit tier starts at 51.
        >= 10 => 0.05m,
        _ => 0m,
    };

    /// <summary>The better of the loyalty and the bulk discount - they do not stack.</summary>
    public static decimal Discount(Order order) =>
        Money.Round(order.Subtotal * Math.Max(LoyaltyRate(order.Customer), BulkRate(order.Units)), "EUR");

    public static readonly IReadOnlyDictionary<string, decimal> VatRates = new Dictionary<string, decimal>
    {
        ["DE"] = 0.19m, ["FR"] = 0.20m, ["PL"] = 0.23m, ["UA"] = 0.20m, ["NL"] = 0.21m,
        ["ES"] = 0.21m, ["IT"] = 0.22m, ["SE"] = 0.25m, ["DK"] = 0.25m, ["HU"] = 0.27m,
        ["AT"] = 0.20m, ["BE"] = 0.21m, ["CZ"] = 0.21m, ["FI"] = 0.255m, ["IE"] = 0.23m,
        ["PT"] = 0.23m, ["RO"] = 0.19m, ["SK"] = 0.23m, ["SI"] = 0.22m, ["LT"] = 0.21m,
        ["LV"] = 0.21m, ["EE"] = 0.24m, ["GR"] = 0.24m, ["HR"] = 0.25m, ["LU"] = 0.17m,
    };

    public static decimal Vat(decimal net, string country) =>
        Money.Round(net * (VatRates.TryGetValue(country, out var rate)
            ? rate
            : throw new NotSupportedException($"No VAT rate for country '{country}'.")), "EUR");

    public static decimal GiftWrapFee(Order order) =>
        // Bug: the fee is charged per unit instead of once per order.
        order.GiftWrap ? 3.50m * order.Units : 0m;
}

public static class Money
{
    public static int Decimals(string currency) => currency switch
    {
        "JPY" => 0,
        "KWD" => 2,             // Bug: the Kuwaiti dinar has three decimals.
        _ => 2,
    };

    public static decimal Round(decimal amount, string currency) =>
        Math.Round(amount, Decimals(currency), MidpointRounding.AwayFromZero);
}

public static class Shipping
{
    public const decimal FreeShippingThreshold = 100m;

    /// <summary>Zone base price + per-kg price, rounded to cents. Orders of at least 100 EUR ship free.</summary>
    public static decimal Cost(string zone, decimal weightKg, decimal subtotal = 0m)
    {
        if (subtotal > FreeShippingThreshold) return 0m;    // Bug: exactly 100 EUR should ship free too.
        return Money.Round(BasePrice(zone) + PerKg(zone) * weightKg, "EUR");
    }

    public static decimal BasePrice(string zone) => zone switch
    {
        "LOCAL" => 2.90m, "EU" => 6.90m, "UK" => 9.90m, "US" => 14.90m, "ASIA" => 17.90m,
        _ => throw new NotSupportedException($"Unknown shipping zone '{zone}'."),
    };

    public static decimal PerKg(string zone) => zone switch
    {
        "LOCAL" => 0.50m, "EU" => 1.20m, "UK" => 1.80m, "US" => 2.40m, "ASIA" => 3.10m,
        _ => throw new NotSupportedException($"Unknown shipping zone '{zone}'."),
    };

    public static string ZoneOf(string country) => country switch
    {
        "DE" => "LOCAL",
        "GB" => "UK",
        "US" => "US",
        "JP" or "CN" or "KR" => "ASIA",
        _ => "EU",
    };
}

/// <summary>
/// The carrier's live rate API. It is a remote dependency: on even builds its first call times out (the
/// scenarios using it are tagged @retry and recover), and during build 3 it is down for good.
/// </summary>
public sealed class CarrierRateApi
{
    // The remote side's state, not the client's: a retry with a fresh client still finds the service warmed up.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> Requests = new();

    public decimal Quote(string zone, decimal weightKg)
    {
        var calls = Requests.AddOrUpdate($"{zone}/{weightKg}", 1, (_, n) => n + 1);
        if (ShowcaseBuild.Number == 3)
            throw new HttpRequestException("Carrier rate API unavailable (503 Service Unavailable).");
        if (ShowcaseBuild.Number % 2 == 0 && calls == 1)
            throw new TimeoutException("Carrier rate API did not answer within 2 s.");
        return Shipping.Cost(zone, weightKg);
    }
}

public sealed class Inventory
{
    private readonly Dictionary<string, int> _stock = Catalog.Products.ToDictionary(p => p.Sku, _ => 25);

    public int Available(string sku) => _stock[sku];

    public void Reserve(string sku, int quantity)
    {
        // Regression introduced in build 4: reservations no longer check the stock level.
        if (ShowcaseBuild.Number < 4 && quantity > _stock[sku])
            throw new InvalidOperationException($"Only {_stock[sku]} x {sku} in stock.");
        _stock[sku] -= quantity;
    }
}

public static class Recommendations
{
    /// <summary>"Customers also bought" - fixed in build 3; before that it recommended the product itself.</summary>
    public static IReadOnlyList<string> For(string sku)
    {
        var product = Catalog.Find(sku);
        var related = Catalog.Products.Where(p => p.Category == product.Category);
        if (ShowcaseBuild.Number >= 3) related = related.Where(p => p.Sku != sku);
        return [.. related.Select(p => p.Sku).Take(3)];
    }
}

public static class SearchSuggestions
{
    /// <summary>Autocomplete backed by a search cluster whose index is rebuilt during builds 2 and 4 - by the time
    /// the pipeline reruns the failed scenarios the rebuild has finished.</summary>
    public static IReadOnlyList<string> Suggest(string prefix)
    {
        if (ShowcaseBuild.Number is 2 or 4 && !ShowcaseBuild.IsRerun)
            return [];
        return [.. Catalog.Products.Where(p => p.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(p => p.Name)];
    }
}
