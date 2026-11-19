using System;
using System.IO;
using Shouldly;
using Ohirok.PluginChains.Common.Reporting;

namespace StepHashtagsNUnitTests.StepDefinitions
{
    [Binding]
    public class IgnoreStepSteps(IReqnrollOutputHelper output)
    {
        private const string OnePixelPngBase64 =
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

        [Given("this step always executes")]
        [Then("this step always executes")]
        public void AlwaysExecutes()
        {
        }

        [Given("this step should never execute")]
        public void NeverExecutes()
        {
            throw new ShouldAssertException("This step should have been skipped via the #ignore hashtag.");
        }

        [Given("a real image is embedded")]
        public void EmbedRealImage()
        {
            // Not deleted immediately - AddAttachment's base64 encoding happens lazily on
            // FormatterBase's background channel, so deleting right after this call would race it.
            var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.png");
            File.WriteAllBytes(path, Convert.FromBase64String(OnePixelPngBase64));
            output.EmbedImage(path);
        }
    }
}
