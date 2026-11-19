namespace Ohirok.Retry.Core
{
    public static class RetryConstants
    {
        public const string RetryTag = "retry";
        public const string NoRetryTag = "noretry";
        public const string RetryBlockTag = "retryblock";
        public const string EndBlockTag = "end";
        public const string IgnoreTag = "ignore";
        public const string AllowRetryHashtagsTag = "allowretryhashtags";

        public const string AttemptsPrefix = "attempts";
        public const string TimeoutPrefix = "timeout";
        public const string DelayPrefix = "delay";
        public const string ConditionPrefix = "condition";

        public const string RetryLoopStartedEventKey = "RetryLoopStarted";
        public const string RetryRestartedEventKey = "RetryRestarted";
        public const string RetryLoopCompletedEventKey = "RetryLoopCompleted";
        public const string RetryDecisionEventKey = "RetryDecision";
    }
}
