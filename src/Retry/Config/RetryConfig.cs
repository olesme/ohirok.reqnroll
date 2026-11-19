using Ohirok.PluginChains.Common;

namespace Ohirok.Retry.Config
{
    public static class RetryConfig
    {
        private static RetrySection _cached;
        private static RetrySection Section => _cached ??= PluginConfigProvider.GetSection<RetrySection>("retry");

        public static int MaxAttempts => Section.MaxAttempts;

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
