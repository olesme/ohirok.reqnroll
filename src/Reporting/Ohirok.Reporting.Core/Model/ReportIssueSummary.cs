using Ohirok.PluginChains.Common.Reporting;

namespace Ohirok.Reporting.Model
{
    public class ReportIssueSummary
    {
        public string SourcePlugin { get; set; } = "";
        public ReportIssueCategory Category { get; set; }
        public string? Severity { get; set; }
        public string Message { get; set; } = "";
        public List<string> Tags { get; set; } = [];
        public List<string> Hashtags { get; set; } = [];
    }
}
