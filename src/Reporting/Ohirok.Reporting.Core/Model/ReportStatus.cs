namespace Ohirok.Reporting.Model
{
    public enum ReportStatus
    {
        Unknown,
        Passed,
        PassWithIssues,
        Skipped,
        Pending,
        Undefined,
        Ambiguous,
        Failed,

        /// <summary>Scenario was defined in the Gherkin document but the pickle-processing pass never produced a TestCase for it (excluded from the run) - see EnvelopeModelBuilder.</summary>
        NotRun
    }
}
