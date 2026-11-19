namespace Ohirok.Reporting.Model
{
    public class ReportStep
    {
        public string Keyword { get; set; } = "";
        public string Text { get; set; } = "";
        public ReportStatus Status { get; set; } = ReportStatus.Unknown;
        public double DurationMs { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>Full exception stack trace (Cucumber Messages TestStepResult.Exception.StackTrace), distinct from the short <see cref="ErrorMessage"/> already shown by default - report.js reveals this behind a "Show full trace" toggle.</summary>
        public string? StackTrace { get; set; }
        public List<ReportAttachment> Attachments { get; set; } = [];
        public List<string> Hashtags { get; set; } = [];
        public ReportStepArgument? Argument { get; set; }
        public List<ReportHook> BeforeHooks { get; set; } = [];
        public List<ReportHook> AfterHooks { get; set; } = [];

        /// <summary>Matched step-definition's raw Cucumber-expression/regex text (Cucumber Messages StepDefinition.Pattern.Source) - lets report.js match search input containing placeholder syntax (e.g. "I {word} smth") against every instantiated occurrence regardless of concrete arguments. Null for steps with no resolved step definition (undefined/ambiguous) or template steps built outside the per-execution pass.</summary>
        public string? Pattern { get; set; }

        /// <summary>Full text of the .feature-file comment line(s) immediately preceding this step (not just the extracted #hashtag tokens already captured in <see cref="Hashtags"/>), so report.js can display the comment itself above the step. Null when the step has no preceding comment.</summary>
        public string? Comment { get; set; }
    }
}
