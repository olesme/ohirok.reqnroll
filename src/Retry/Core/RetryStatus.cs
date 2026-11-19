namespace Ohirok.Retry.Core
{
    public enum RetryStatus
    {
        None = 0,
        Running = 1,
        Success = 2,
        Failed = 3,
        TimedOut = 4
    }
}
