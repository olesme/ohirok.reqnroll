namespace Ohirok.Reporting.Model
{
    public class ReportRule
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = [];

        /// <summary>Full text of the .feature-file comment line(s) immediately preceding the Rule header - see StaticHashtagAssociator.</summary>
        public string? Comment { get; set; }

        /// <summary>Steps only, no status - a Rule-level Background has no independent pass/fail (Gherkin model).</summary>
        public List<ReportStep> Background { get; set; } = [];
        public string? BackgroundName { get; set; }
        public string? BackgroundDescription { get; set; }
        public List<ReportScenario> Scenarios { get; set; } = [];
        public List<ReportScenarioOutline> ScenarioOutlines { get; set; } = [];
    }
}
