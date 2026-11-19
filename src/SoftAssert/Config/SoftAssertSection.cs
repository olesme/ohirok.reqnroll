using System.Text.Json.Serialization;

namespace Ohirok.SoftAssert.Config
{
    public class SoftAssertSection
    {
        [JsonPropertyName("generationInterceptorOrder")]
        public int GenerationInterceptorOrder { get; set; } = 100;

        // Lower = more outer. Must stay outside step-level Retry (0): Retry then sees the raw step
        // exception and SoftAssert captures only what is left after retries.
        [JsonPropertyName("bindingInvokerOrder")]
        public int BindingInvokerOrder { get; set; } = -1000;
    }
}
