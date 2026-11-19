namespace Ohirok.Reporting.Model
{
    public class ReportScenario
    {
        public string Name { get; set; } = "";

        /// <summary>Raw un-instantiated Scenario Outline title (with `&lt;placeholder&gt;` tokens) for an example-row scenario - null for a plain (non-outline) scenario, where Name is already literal. Lets report.js match search input written as it appears in the feature file against every row of the outline regardless of the row's actual example values.</summary>
        public string? NameTemplate { get; set; }

        /// <summary>For an outline example row (null for a plain scenario) - the owning ReportExampleSet's column header to this row's literal cell value (examples.TableHeader/TableBody.Cells), in table order. Lets report.js show this row's raw arguments next to its already-substituted Name, and render a real Examples table for the owning ReportExampleSet.</summary>
        public Dictionary<string, string>? ArgumentValues { get; set; }
        public string? Description { get; set; }
        public List<string> Tags { get; set; } = [];

        /// <summary>Full text of the .feature-file comment line(s) immediately preceding this scenario's header (or, for an outline example row, its example-table row) - distinct from any individual step's own Comment. See StaticHashtagAssociator.</summary>
        public string? Comment { get; set; }

        /// <summary>Stable per-scenario identity across runs - featureUri::scenarioName, or featureUri::[rule::]outlineName::exampleSetName::rowIndex for outline rows. Used as the run-history storage key (Ohirok.Reporting.History); never rendered.</summary>
        public string? ScenarioKey { get; set; }

        /// <summary>Prior runs (including Incomplete, part of the "W-minimal" outcome widening), newest first, shipped at
        /// max(HistoryWindow, HistoryDisplayWindow) rather than trimmed to just the display window -
        /// null when no history backend is configured. Powers report.js's per-scenario history-dot
        /// strip and the Analytics History matrix/trend chart, AND feeds the client-side Health computation
        /// (analyzeScenarioHealth in report.js) that replaced this project's former server-side Health field -
        /// see feedback_client_side_computation memory.</summary>
        public List<ReportHistoryRunSummary>? RecentRuns { get; set; }

        /// <summary>Union of every ReportIssue.Hashtags reported for this scenario - hashtags aren't on the wire otherwise (they're a step-comment/runtime-only concept, never merged into Pickle.Tags).</summary>
        public List<string> Hashtags { get; set; } = [];

        /// <summary>Derived overall status - never a flat wire status, see EnvelopeModelBuilder.DeriveScenarioStatus. Defaults to NotRun: the pickle-processing pass overwrites this for every scenario it actually finds a TestCase for, so a value that survives untouched means the scenario was excluded from the run.</summary>
        public ReportStatus Status { get; set; } = ReportStatus.NotRun;

        /// <summary>Ordered oldest-to-newest; a scenario that never retried has exactly one entry.</summary>
        public List<ReportAttempt> Attempts { get; set; } = [];
        public List<ReportIssueSummary> Issues { get; set; } = [];

        /// <summary>Per configured reporting.filters group, the matched option's label/color (or the group's implicit "Other" bucket) - see ReportFilterMatcher.</summary>
        public List<ReportFilterMatch> FilterMatches { get; set; } = [];

        public ReportAttempt? FinalAttempt => Attempts.Count > 0 ? Attempts[Attempts.Count - 1] : null;
        public List<ReportStep> Steps => FinalAttempt?.Steps ?? [];
        public List<ReportAttachment> Attachments => FinalAttempt?.Attachments ?? [];
        public List<ReportHook> Hooks => FinalAttempt?.Hooks ?? [];
    }
}
