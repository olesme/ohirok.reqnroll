namespace Ohirok.Reporting.Model
{
    public class ReportScenarioOutline
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = [];

        /// <summary>Full text of the .feature-file comment line(s) immediately preceding the Scenario Outline header - see StaticHashtagAssociator.</summary>
        public string? Comment { get; set; }

        public List<ReportStep> StepTemplates { get; set; } = [];
        public List<ReportExampleSet> ExampleSets { get; set; } = [];
    }
}
