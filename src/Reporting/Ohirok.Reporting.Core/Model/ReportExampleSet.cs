namespace Ohirok.Reporting.Model
{
    public class ReportExampleSet
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = [];

        /// <summary>Full text of the .feature-file comment line(s) immediately preceding the Examples header - see StaticHashtagAssociator.</summary>
        public string? Comment { get; set; }

        /// <summary>Column headers of the .feature file's Examples table (examples.TableHeader.Cells), in order - empty if the Examples block has no header row. Paired by index with each ReportScenario.ArgumentValues key to render a real Examples table in report.js.</summary>
        public List<string> Headers { get; set; } = [];

        public List<ReportScenario> Scenarios { get; set; } = [];
    }
}
