using Ohirok.PluginChains.Common;

namespace Ohirok.Retry.Config
{
    public static class RetryConfig
    {
        private static RetrySection _cached;
        private static RetrySection Section => _cached ??= PluginConfigProvider.GetSection<RetrySection>("retry");

        /// <summary>The default total runs. retry.maxAttempts and retry.maxRetries are two spellings of this one value;
        /// when both are set they must agree.</summary>
        public static int MaxAttempts
        {
            get
            {
                var s = Section;
                int? fromRetries = s.MaxRetries.HasValue ? Math.Max(0, s.MaxRetries.Value) + 1 : null;
                int? fromAttempts = s.MaxAttempts.HasValue ? Math.Max(1, s.MaxAttempts.Value) : null;
                if (fromRetries.HasValue && fromAttempts.HasValue && fromRetries != fromAttempts)
                    throw new InvalidOperationException(
                        $"reqnroll.json: retry.maxAttempts ({s.MaxAttempts}) and retry.maxRetries ({s.MaxRetries}) disagree. " +
                        "They are the same setting counted two ways (attempts = retries + 1); set only one.");
                return fromAttempts ?? fromRetries ?? 3;
            }
        }

        public static string[] Conditions
        {
            get
            {
                var s = Section;
                if (s.Conditions != null && s.Conditions.Length > 0)
                    return s.Conditions;
                if (!string.IsNullOrEmpty(s.Condition))
                    return new[] { s.Condition };
                return Array.Empty<string>();
            }
        }

        public static TimeSpan Delay => TimeSpan.FromMilliseconds(Section.DelayMs);
        public static bool AllowRetryTag => Section.AllowRetryTag;
        public static bool AllowRetryHashtag => Section.AllowRetryHashtag;
        public static bool AllowRetryStepAttribute => Section.AllowRetryStepAttribute;
        public static bool PublishEvents => Section.PublishEvents;
        public static int GenerationInterceptorOrder => Section.GenerationInterceptorOrder;
        public static int BindingInvokerOrder => Section.BindingInvokerOrder;
    }
}
