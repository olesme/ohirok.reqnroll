using Ohirok.Retry.Attributes;
using Ohirok.Retry.Core;

namespace RetryTests.Hooks
{
    /// <summary>
    /// Named retry policies: the tag names the policy, the hook gives it its settings. A BeforeRetryLoop hook runs after
    /// the tags were read and before the first retry decision, so what it sets applies to the whole loop.
    /// </summary>
    [Binding]
    public sealed class RetryPolicies
    {
        [BeforeRetryLoop("@retry:flaky", "#retry:flaky", "#retryblock:flaky")]
        public void Flaky(RetryContext retry)
        {
            retry.MaxAttempts = 5;
            retry.Delay = TimeSpan.FromMilliseconds(50);
        }

        [BeforeRetryLoop("@retry:payments")]
        public void Payments(RetryContext retry)
        {
            retry.MaxAttempts = 3;
            retry.Conditions = ["TimeoutException", "HttpRequestException"];
        }
    }
}
