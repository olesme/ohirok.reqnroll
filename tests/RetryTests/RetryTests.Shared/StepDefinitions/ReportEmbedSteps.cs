using Ohirok.PluginChains.Common.Reporting;

namespace RetryTests.StepDefinitions
{
    [Binding]
    public sealed class ReportEmbedSteps(IReqnrollOutputHelper output)
    {
        [Given("an image URL is embedded")]
        public void EmbedImageUrl() =>
            output.EmbedImageUrl("https://reqnroll.net/img/reqnroll_logo_readme.png", "Reqnroll logo");

        [Given("an error callout is embedded")]
        public void EmbedErrorCallout() =>
            output.EmbedError("<strong>Simulated error</strong> for D10 embed-marker verification.");

        [Given("a warning callout is embedded")]
        public void EmbedWarningCallout() =>
            output.EmbedWarning("<strong>Simulated warning</strong> for D10 embed-marker verification.");

        [Given("an info callout is embedded")]
        public void EmbedInfoCallout() =>
            output.EmbedInfo("<strong>Simulated info</strong> for D10 embed-marker verification.");

        [Given("raw HTML is embedded")]
        public void EmbedRawHtml() =>
            output.EmbedHtml("<div style=\"font-weight:bold;color:#1e88e5;\">Raw HTML embed, rendered unescaped.</div>");
    }
}
