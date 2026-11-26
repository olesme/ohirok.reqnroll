namespace Ohirok.PluginChains.Common.Reporting
{
    /// <summary>
    /// Why a scenario that otherwise passed still deserves a "Pass With Issues" signal in a future reporting formatter.
    /// </summary>
    public enum ReportIssueCategory
    {
        RetrySucceeded,
        SoftAssertUnflushed,
        XFailPassed,
        StepSkipped,
        Custom,
        /// <summary>A retry loop stopped because its time budget ran out (the scenario failed).</summary>
        RetryTimedOut,
        /// <summary>A scenario or step failed the way its @xfail / @afail tag or #xfail / #afail hashtag expects or allows;
        /// the test itself passed.</summary>
        ExpectedFailure
    }
}
