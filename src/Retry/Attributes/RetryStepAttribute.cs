using Ohirok.Retry.Config;
using Ohirok.Retry.Core;
using Reqnroll.Infrastructure;

namespace Ohirok.Retry.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RetryStepAttribute : Attribute
    {
        /// <summary>Total runs, the first one included. 0 (unset): the configuration default.</summary>
        public int MaxAttempts { get; set; }

        /// <summary>Retries after the first run; 0 means no retry. -1 (unset): the configuration default. Set this or
        /// <see cref="MaxAttempts"/>, not both.</summary>
        public int MaxRetries { get; set; } = -1;

        public string Condition { get; set; }
        public string[] Conditions { get; set; }
        public double TimeoutMs { get; set; }
        public double DelayMs { get; set; }
        public string[] Tags { get; set; }

        public RetryStepAttribute() { }

        internal RetryContext CreateContext(IContextManager ctxMgr)
        {
            var ctx = new RetryContext
            {
                Type = RetryType.Step,
                MaxAttempts = ResolveMaxAttempts(),
                Timeout = TimeoutMs > 0 ? TimeSpan.FromMilliseconds(TimeoutMs) : TimeSpan.Zero,
                Delay = DelayMs > 0 ? TimeSpan.FromMilliseconds(DelayMs) : RetryConfig.Delay
            };

            if (Conditions != null && Conditions.Length > 0)
                ctx.Conditions = Conditions;
            else if (!string.IsNullOrEmpty(Condition))
                ctx.Conditions = [Condition];
            else
                ctx.Conditions = RetryConfig.Conditions;

            ctx.InitFromScenarioContext(ctxMgr.ScenarioContext, Tags);
            return ctx;
        }

        private int ResolveMaxAttempts()
        {
            if (MaxRetries >= 0 && MaxAttempts > 0 && MaxRetries + 1 != MaxAttempts)
                throw new InvalidOperationException(
                    $"[RetryStep]: MaxAttempts = {MaxAttempts} and MaxRetries = {MaxRetries} disagree (attempts = retries + 1); set only one.");
            if (MaxRetries >= 0) return MaxRetries + 1;
            return MaxAttempts > 0 ? MaxAttempts : RetryConfig.MaxAttempts;
        }
    }
}
