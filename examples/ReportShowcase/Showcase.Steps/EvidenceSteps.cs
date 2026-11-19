using System.Globalization;
using System.Net;
using System.Text;
using Ohirok.PluginChains.Common.Reporting;
using Reqnroll;
using Showcase.Domain;

namespace Showcase.Steps;

/// <summary>
/// Everything a test can attach to the Ohirok report: images (inline bytes and URLs), raw HTML, callouts and links
/// to work items. Screenshots are rendered as SVG from the real order, so they show what the test saw.
/// </summary>
[Binding]
public sealed class EvidenceSteps(ShopContext shop, ScenarioContext scenario, IReqnrollOutputHelper output)
{
    // Placeholders - point them at your own tracker (GitHub Issues, Azure DevOps, Jira, ...).
    private const string BugUrl = "https://github.com/your-org/tea-shop/issues/{0}";
    private const string BacklogItemUrl = "https://dev.azure.com/your-org/tea-shop/_workitems/edit/{0}";
    private const string NewBugUrl = "https://github.com/your-org/tea-shop/issues/new?title={0}";

    [When("a screenshot of the cart is attached")]
    public void CartScreenshot() => output.EmbedImage(Encoding.UTF8.GetBytes(CartSvg(shop.Order, "Cart")), ".svg");

    [When("a screenshot of the cart is attached, expanded")]
    public void CartScreenshotExpanded() =>
        output.EmbedImage(Encoding.UTF8.GetBytes(CartSvg(shop.Order, "Cart")), ".svg", expanded: true);

    [When("the product photo of {word} is attached")]
    public void ProductPhoto(string sku) =>
        output.EmbedImageUrl($"https://placehold.co/480x320/png?text={Uri.EscapeDataString(Catalog.Find(sku).Name)}",
            altText: Catalog.Find(sku).Name);

    [Then("the receipt is attached as a table")]
    public void ReceiptTable()
    {
        var o = shop.Order;
        var rows = string.Concat(o.Lines.Select(l =>
            $"<tr><td>{Enc(l.Product.Name)}</td><td>{l.Quantity}</td><td>{Eur(l.Product.Price)}</td><td>{Eur(l.Total)}</td></tr>"));
        var discount = Pricing.Discount(o);
        var shipping = Shipping.Cost(Shipping.ZoneOf(o.Country), o.WeightKg, o.Subtotal);
        output.EmbedHtml(
            "<table><thead><tr><th>Item</th><th>Qty</th><th>Price</th><th>Total</th></tr></thead><tbody>" + rows +
            $"<tr><td colspan=\"3\">Discount</td><td>-{Eur(discount)}</td></tr>" +
            $"<tr><td colspan=\"3\">Shipping to {Enc(o.Country)}</td><td>{Eur(shipping)}</td></tr>" +
            $"<tr><th colspan=\"3\">To pay</th><th>{Eur(o.Subtotal - discount + shipping)}</th></tr></tbody></table>",
            expanded: true);
    }

    [Then("an info note says {string}")]
    public void Info(string text) => output.EmbedInfo(text);

    [Then("a warning says {string}")]
    public void Warning(string text) => output.EmbedWarning(text);

    [Then("an error callout says {string}")]
    public void Error(string text) => output.EmbedError(text);

    [Then("the deployment that was verified is linked")]
    public void DeploymentLink() =>
        output.EmbedHtml(Link($"https://github.com/your-org/tea-shop/actions/runs/{9_000_000 + ShowcaseBuild.Number}",
            $"Verified deployment of build {ShowcaseBuild.Number}"));

    /// <summary>@bug:&lt;id&gt; and @pbi:&lt;id&gt; tags become links; a failed scenario also gets a screenshot of the
    /// state it failed in and a prefilled "report a bug" link.</summary>
    [AfterScenario(Order = 0)]
    public void AttachWorkItemsAndFailureEvidence()
    {
        var links = new List<string>();
        foreach (var tag in scenario.ScenarioInfo.Tags)
        {
            if (TryGetId(tag, "bug:", out var bug)) links.Add(Link(string.Format(BugUrl, bug), $"Known bug #{bug}"));
            else if (TryGetId(tag, "pbi:", out var pbi)) links.Add(Link(string.Format(BacklogItemUrl, pbi), $"Backlog item #{pbi}"));
        }

        if (scenario.TestError != null)
        {
            if (shop.Order.Lines.Count > 0)
                output.EmbedImage(Encoding.UTF8.GetBytes(CartSvg(shop.Order, "State at failure")), ".svg");
            links.Add(Link(string.Format(NewBugUrl, Uri.EscapeDataString($"Failing: {scenario.ScenarioInfo.Title}")), "Report a bug"));
        }

        if (links.Count > 0)
            output.EmbedHtml(string.Join(" &middot; ", links), expanded: true);
    }

    private static bool TryGetId(string tag, string prefix, out string id)
    {
        id = tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? tag[prefix.Length..] : "";
        return id.Length > 0;
    }

    private static string Link(string url, string text) =>
        $"<a href=\"{WebUtility.HtmlEncode(url)}\" target=\"_blank\" rel=\"noopener\">{WebUtility.HtmlEncode(text)}</a>";

    private static string Enc(string s) => WebUtility.HtmlEncode(s);

    private static string Eur(decimal v) => v.ToString("0.00", CultureInfo.InvariantCulture) + " EUR";

    /// <summary>A small "browser window" with the cart lines - a stand-in for a real UI screenshot.</summary>
    private static string CartSvg(Order order, string caption)
    {
        var height = 110 + order.Lines.Count * 28;
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"520\" height=\"{height}\" font-family=\"Segoe UI, Arial, sans-serif\">");
        sb.Append($"<rect width=\"520\" height=\"{height}\" rx=\"8\" fill=\"#ffffff\" stroke=\"#c7c7c7\"/>");
        sb.Append("<rect width=\"520\" height=\"34\" rx=\"8\" fill=\"#ececec\"/><circle cx=\"18\" cy=\"17\" r=\"6\" fill=\"#ff5f57\"/>");
        sb.Append("<circle cx=\"38\" cy=\"17\" r=\"6\" fill=\"#febc2e\"/><circle cx=\"58\" cy=\"17\" r=\"6\" fill=\"#28c840\"/>");
        sb.Append($"<text x=\"80\" y=\"22\" font-size=\"13\" fill=\"#555\">tea-shop.example/cart &#8212; {Enc(caption)} (build {ShowcaseBuild.Number})</text>");
        var y = 64;
        foreach (var line in order.Lines)
        {
            sb.Append($"<text x=\"20\" y=\"{y}\" font-size=\"14\" fill=\"#222\">{line.Quantity} &#215; {Enc(line.Product.Name)}</text>");
            sb.Append($"<text x=\"500\" y=\"{y}\" font-size=\"14\" fill=\"#222\" text-anchor=\"end\">{Eur(line.Total)}</text>");
            y += 28;
        }
        sb.Append($"<line x1=\"20\" y1=\"{y - 12}\" x2=\"500\" y2=\"{y - 12}\" stroke=\"#ddd\"/>");
        sb.Append($"<text x=\"20\" y=\"{y + 14}\" font-size=\"15\" font-weight=\"bold\" fill=\"#222\">Subtotal</text>");
        sb.Append($"<text x=\"500\" y=\"{y + 14}\" font-size=\"15\" font-weight=\"bold\" fill=\"#222\" text-anchor=\"end\">{Eur(order.Subtotal)}</text>");
        sb.Append("</svg>");
        return sb.ToString();
    }
}
