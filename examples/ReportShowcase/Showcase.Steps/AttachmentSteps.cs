using System.IO.Compression;
using System.Text;
using Ohirok.PluginChains.Common.Reporting;
using Reqnroll;

namespace Showcase.Steps;

/// <summary>
/// One step per kind of attachment, so the report's rendering of each can be seen side by side: Reqnroll's own
/// <see cref="IReqnrollOutputHelper.AddAttachment"/> (files of several types) and <c>WriteLine</c>, and the Ohirok
/// embeds (inline images, image URLs, HTML, callouts).
/// </summary>
[Binding]
public sealed class AttachmentSteps(IReqnrollOutputHelper output, ScenarioContext scenario)
{
    private string WriteFile(string name, byte[] content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "ohirok-showcase", scenario.ScenarioInfo.Title.GetHashCode().ToString("x"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    [When("a line of text is written to the output")]
    public void TextOutput() => output.WriteLine("Order 10042 submitted at 10:42:07; payment reference PAY-778812.");

    [When("a PNG file is attached the Reqnroll way")]
    public void PngFile() => output.AddAttachment(WriteFile("chart.png", Png.Bars(360, 160, [3, 7, 5, 9, 4, 8])));

    [When("a text file is attached the Reqnroll way")]
    public void TextFile() => output.AddAttachment(WriteFile("server.log",
        Encoding.UTF8.GetBytes("10:42:06 INFO  checkout started\n10:42:07 INFO  payment authorized\n10:42:07 WARN  carrier quote slow (1.8 s)\n")));

    [When("a JSON file is attached the Reqnroll way")]
    public void JsonFile() => output.AddAttachment(WriteFile("order.json",
        Encoding.UTF8.GetBytes("{\n  \"id\": 10042,\n  \"lines\": [{ \"sku\": \"TEA-001\", \"quantity\": 2 }],\n  \"total\": 25.00\n}\n")));

    [When("an HTML file is attached the Reqnroll way")]
    public void HtmlFile() => output.AddAttachment(WriteFile("invoice.html",
        Encoding.UTF8.GetBytes("<html><body><h1>Invoice 10042</h1><p>2 x Sencha green tea - 25.00 EUR</p></body></html>")));

    [When("an image is embedded inline")]
    public void InlinePng() => output.EmbedImage(Png.Bars(360, 160, [9, 4, 6, 2, 7, 5]), ".png");

    [When("an image is embedded inline, expanded")]
    public void InlinePngExpanded() => output.EmbedImage(Png.Bars(360, 160, [2, 5, 8, 6, 9, 3]), ".png", expanded: true);

    [When("an image is embedded by URL")]
    public void ImageUrl() => output.EmbedImageUrl("https://placehold.co/480x240/png?text=Image+by+URL", "Image by URL");

    [When("HTML with a table and links is embedded")]
    public void Html() => output.EmbedHtml(
        "<table><thead><tr><th>Check</th><th>Result</th></tr></thead><tbody>" +
        "<tr><td>Price</td><td>ok</td></tr><tr><td>Stock</td><td>ok</td></tr></tbody></table>" +
        "<p><a href=\"https://github.com/your-org/tea-shop/issues/418\" target=\"_blank\" rel=\"noopener\">Known bug #418</a></p>");

    [When("an info, a warning and an error callout are embedded")]
    public void Callouts()
    {
        output.EmbedInfo("The order was placed in the EU warehouse.");
        output.EmbedWarning("The carrier answered in 1.8 s (limit 2 s).");
        output.EmbedError("The invoice PDF could not be generated (shown here as a demo, the step passes).");
    }

    [AfterStep("attachment-in-hook")]
    public void AfterStepAttachment() =>
        output.WriteLine($"After-step hook: state after '{scenario.StepContext.StepInfo.Text}' recorded.");
}

/// <summary>A tiny PNG writer, so the showcase needs no image library: draws a bar chart.</summary>
internal static class Png
{
    public static byte[] Bars(int width, int height, int[] values)
    {
        var max = values.Max();
        var raw = new byte[height * (width * 3 + 1)];
        var barWidth = width / values.Length;
        for (var y = 0; y < height; y++)
        {
            var row = y * (width * 3 + 1);
            raw[row] = 0;
            for (var x = 0; x < width; x++)
            {
                var bar = Math.Min(x / barWidth, values.Length - 1);
                var top = height - height * values[bar] / max * 9 / 10;
                var inBar = y >= top && x % barWidth > 4;
                var (r, g, b) = inBar ? ((byte)47, (byte)122, (byte)69) : ((byte)246, (byte)241, (byte)231);
                raw[row + 1 + x * 3] = r; raw[row + 2 + x * 3] = g; raw[row + 3 + x * 3] = b;
            }
        }
        using var ms = new MemoryStream();
        ms.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        Chunk(ms, "IHDR", [.. BigEndian(width), .. BigEndian(height), 8, 2, 0, 0, 0]);
        using (var z = new MemoryStream())
        {
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, leaveOpen: true)) zs.Write(raw);
            Chunk(ms, "IDAT", z.ToArray());
        }
        Chunk(ms, "IEND", []);
        return ms.ToArray();
    }

    private static byte[] BigEndian(int v) => [(byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v];

    private static void Chunk(Stream s, string type, byte[] data)
    {
        s.Write(BigEndian(data.Length));
        var typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(data);
        s.Write(BigEndian((int)Crc([.. typeBytes, .. data])));
    }

    private static uint Crc(byte[] bytes)
    {
        var c = 0xFFFFFFFFu;
        foreach (var b in bytes)
        {
            c ^= b;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }
        return c ^ 0xFFFFFFFFu;
    }
}
