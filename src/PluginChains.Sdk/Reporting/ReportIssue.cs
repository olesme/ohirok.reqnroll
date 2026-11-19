namespace Ohirok.PluginChains.Common.Reporting
{
    /// <summary>
    /// Structured signal emitted by a plugin (Retry/SoftAssert/XFail/StepHashtags/custom) describing why a scenario
    /// that otherwise passed should still be flagged. Carried through <see cref="Reqnroll.IReqnrollOutputHelper.WriteLine(string)"/>
    /// as an <see cref="ReportMarkers.IssueTag"/> tag so a future reporting formatter can recover it from the wire.
    /// </summary>
    public sealed class ReportIssue
    {
        public string SourcePlugin { get; set; } = "";
        public ReportIssueCategory Category { get; set; }
        public string? Severity { get; set; }
        public string Message { get; set; } = "";
        public string[]? Tags { get; set; }
        public string[]? Hashtags { get; set; }
    }
}
