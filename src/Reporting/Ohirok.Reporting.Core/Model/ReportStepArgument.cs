namespace Ohirok.Reporting.Model
{
    /// <summary>A step's DocString or DataTable argument - static Gherkin syntax, derived from the AST, never
    /// runtime-computed.</summary>
    public class ReportStepArgument
    {
        /// <summary>"docstring" or "datatable".</summary>
        public string Kind { get; set; } = "";
        public string? MediaType { get; set; }
        public string? Text { get; set; }
        public List<List<string>>? Rows { get; set; }
    }
}
