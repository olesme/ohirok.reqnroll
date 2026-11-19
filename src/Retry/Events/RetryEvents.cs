using Ohirok.Retry.Core;
using Reqnroll.Events;

namespace Ohirok.Retry.Events
{
    public abstract class RetryEvent(RetryContext context) : ExecutionEvent
    {
        public RetryContext Context { get; } = context;
        public RetryType RetryType => Context?.Type ?? RetryType.None;
        public RetryStatus Status => Context?.Status ?? RetryStatus.None;
    }

    public sealed class RetryLoopStartedEvent(RetryContext context) : RetryEvent(context) { }

    public sealed class RetryRestartedEvent(RetryContext context) : RetryEvent(context) { }

    public sealed class RetryLoopCompletedEvent(RetryContext context) : RetryEvent(context) { }

    public sealed class RetryDecisionEvent(RetryContext context) : RetryEvent(context) { }
}
