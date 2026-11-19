using System.Text.Json.Serialization;

namespace Ohirok.Retry.Config
{
    public class RetrySection
    {
        [JsonPropertyName("maxAttempts")]
        public int MaxAttempts { get; set; } = 3;

        [JsonPropertyName("condition")]
        public string Condition { get; set; } = string.Empty;

        [JsonPropertyName("conditions")]
        public string[] Conditions { get; set; } = [];

        [JsonPropertyName("delay")]
        public double DelayMs { get; set; } = 0;

        [JsonPropertyName("allowRetryTag")]
        public bool AllowRetryTag { get; set; } = true;

        [JsonPropertyName("allowRetryHashtag")]
        public bool AllowRetryHashtag { get; set; } = true;

        [JsonPropertyName("allowRetryStepAttribute")]
        public bool AllowRetryStepAttribute { get; set; } = true;

        [JsonPropertyName("publishEvents")]
        public bool PublishEvents { get; set; } = false;

        [JsonPropertyName("generationInterceptorOrder")]
        public int GenerationInterceptorOrder { get; set; } = 50;

        [JsonPropertyName("bindingInvokerOrder")]
        public int BindingInvokerOrder { get; set; } = 0;
    }
}
