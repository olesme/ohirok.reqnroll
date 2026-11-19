namespace Ohirok.Reporting.Model
{
    /// <summary>One hook execution (BeforeScenario/AfterScenario/BeforeStep/AfterStep) within a single attempt.
    /// Type/SourceReference are the hook's static identity (resolved from its Hook envelope); Status/DurationMs/
    /// Attachments are genuinely runtime, sourced from the hook's own synthetic TestStep execution.</summary>
    public class ReportHook
    {
        /// <summary>"BeforeScenario", "AfterScenario", "BeforeStep", "AfterStep", "BeforeTestRun" or "AfterTestRun".</summary>
        public string Type { get; set; } = "";
        public string? SourceReference { get; set; }
        public ReportStatus Status { get; set; } = ReportStatus.Unknown;
        public double DurationMs { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>Full exception stack trace (Cucumber Messages TestStepResult.Exception.StackTrace), distinct from the short <see cref="ErrorMessage"/> already shown by default - report.js reveals this behind a "Show full trace" toggle.</summary>
        public string? StackTrace { get; set; }
        public List<ReportAttachment> Attachments { get; set; } = [];
    }
}
