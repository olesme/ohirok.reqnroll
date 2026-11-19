namespace Ohirok.Reporting.Model
{
    /// <summary>
    /// Optional historical scenario snapshot embedded in a prior run summary so the client can
    /// render a real historical scenario detail inline instead of a plain status dot.
    /// </summary>
    public class ReportHistoricalScenarioSnapshot
    {
        public string? ScenarioKey { get; set; }
        public string? Status { get; set; }
        public string? Description { get; set; }
        public string? Comment { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }
        public List<ReportStep> Steps { get; set; } = [];
        public List<ReportAttachment> Attachments { get; set; } = [];
        public List<ReportHook> Hooks { get; set; } = [];
        public List<ReportIssueSummary> Issues { get; set; } = [];
        public Dictionary<string, string>? ArgumentValues { get; set; }
    }
}
