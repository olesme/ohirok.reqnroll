using Ohirok.CustomBindings;
using Ohirok.Retry.Core;

namespace Ohirok.Retry.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class RetryHookAttribute : CustomHookAttribute
    {
        public RetryType? FilterByType { get; set; }

        protected RetryHookAttribute(params string[] filters) : base(filters) { }
        protected RetryHookAttribute(RetryType filterByType, params string[] filters) : base(filters)
        {
            FilterByType = filterByType;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class BeforeRetryLoopAttribute : RetryHookAttribute
    {
        public BeforeRetryLoopAttribute(params string[] filters) : base(filters) { Event = RetryConstants.RetryLoopStartedEventKey; }
        public BeforeRetryLoopAttribute(RetryType filterByType, params string[] filters) : base(filterByType, filters) { Event = RetryConstants.RetryLoopStartedEventKey; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnRetryAttribute : RetryHookAttribute
    {
        public OnRetryAttribute(params string[] filters) : base(filters) { Event = RetryConstants.RetryRestartedEventKey; }
        public OnRetryAttribute(RetryType filterByType, params string[] filters) : base(filterByType, filters) { Event = RetryConstants.RetryRestartedEventKey; }
    }

    /// <summary>
    /// Fires right after an attempt has failed, before the built-in retry-or-not verdict is finalized —
    /// earlier than <see cref="OnRetryAttribute"/> (which only fires once a retry is already certain) and
    /// more specific than <see cref="BeforeRetryLoopAttribute"/> (which only knows a loop is possible).
    /// The bound method may inject <see cref="RetryContext"/> and call <see cref="RetryContext.ForceRetry"/>/
    /// <see cref="RetryContext.ForceStop"/> to override the decision for this attempt.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnRetryDecisionAttribute : RetryHookAttribute
    {
        public OnRetryDecisionAttribute(params string[] filters) : base(filters) { Event = RetryConstants.RetryDecisionEventKey; }
        public OnRetryDecisionAttribute(RetryType filterByType, params string[] filters) : base(filterByType, filters) { Event = RetryConstants.RetryDecisionEventKey; }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class AfterRetryLoopAttribute : RetryHookAttribute
    {
        public AfterRetryLoopAttribute(params string[] filters) : base(filters) { Event = RetryConstants.RetryLoopCompletedEventKey; }
        public AfterRetryLoopAttribute(RetryType filterByType, params string[] filters) : base(filterByType, filters) { Event = RetryConstants.RetryLoopCompletedEventKey; }
    }
}
