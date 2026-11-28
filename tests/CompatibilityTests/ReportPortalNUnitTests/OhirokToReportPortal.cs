// The example from the SDK README ("Reporting tools not built on Cucumber Messages"), unchanged: it must compile
// and run next to the real ReportPortal agent. HookProbe below records the same hooks so the scenarios can check
// that they fired.
using Ohirok.Retry.Attributes;
using Ohirok.Retry.Core;
using Ohirok.SoftAssert.Attributes;
using Ohirok.SoftAssert.Core;
using Ohirok.XFail.ReqnrollPlugin;
using Ohirok.XFail.ReqnrollPlugin.Attributes;
using ReportPortal.Shared;
using Reqnroll;

[Binding]
public class OhirokToReportPortal
{
    [OnRetry]
    public void Retried(RetryContext retry) =>
        Context.Current.Log.Warn($"Retry {retry.Type}: attempt {retry.Attempt} failed: {retry.LastException?.Message}");

    [AfterRetryLoop]
    public void RetryFinished(RetryContext retry)
    {
        foreach (var hookFailure in retry.EarlierAttemptHookFailures) Context.Current.Log.Warn(hookFailure);
        Context.Current.Log.Info($"Retry {retry.Type} {retry.Status} after {retry.Attempt} attempts"
            + (retry.IsFailed ? $": {retry.LastFailure?.Message}" : ""));
    }

    [OnXFail, OnAFail]
    public void ExpectedFailure(XFailScenarioContext xfail) =>
        Context.Current.Log.Warn(xfail.Failed
            ? $"{xfail.Mode} failure: {xfail.Exception!.GetType().Name}: {xfail.Exception.Message}"
            : $"{xfail.Mode}: no failure happened");

    [OnSoftFailure]
    public void SoftFailure(SoftAssertContext soft) => Context.Current.Log.Error($"Soft failure: {soft.Error?.Message}");
}

[Binding]
public class HookProbe
{
    public static readonly List<string> Fired = [];

    [OnRetry]
    public void Retried(RetryContext retry) { lock (Fired) Fired.Add("OnRetry"); }

    [OnXFail]
    public void ExpectedFailure(XFailScenarioContext xfail) { lock (Fired) Fired.Add("OnXFail"); }
}
