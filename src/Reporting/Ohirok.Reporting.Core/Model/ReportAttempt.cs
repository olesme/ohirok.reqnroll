namespace Ohirok.Reporting.Model
{
    public class ReportAttempt
    {
        public int AttemptNumber { get; set; } = 1;
        public bool WillBeRetried { get; set; }
        /// <summary>0 for the pipeline's own run; N for the Nth rerun of failed scenarios merged in by Combine.</summary>
        public int Rerun { get; set; }
        public ReportStatus Status { get; set; } = ReportStatus.Unknown;
        public List<ReportStep> Steps { get; set; } = [];
        public List<ReportAttachment> Attachments { get; set; } = [];
        public List<ReportHook> Hooks { get; set; } = [];
    }
}
