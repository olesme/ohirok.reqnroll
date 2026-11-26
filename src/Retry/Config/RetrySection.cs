using System.Text.Json.Serialization;

namespace Ohirok.Retry.Config
{
    public class RetrySection
    {
        /// <summary>Default total runs, the first one included, for a retry tag or attribute without a number.</summary>
        [JsonPropertyName("maxAttempts")]
        public int? MaxAttempts { get; set; }

        /// <summary>The same default counted as retries after the first run. Set one of the two; unset: 2 retries.</summary>
        [JsonPropertyName("maxRetries")]
        public int? MaxRetries { get; set; }

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
