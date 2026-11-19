using Ohirok.Retry.Config;
using Ohirok.Retry.Core;
using Reqnroll.Infrastructure;

namespace Ohirok.Retry.Attributes
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class RetryStepAttribute : Attribute
    {
        public int MaxAttempts { get; set; }
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
                MaxAttempts = MaxAttempts > 0 ? MaxAttempts : RetryConfig.MaxAttempts,
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
    }
}
