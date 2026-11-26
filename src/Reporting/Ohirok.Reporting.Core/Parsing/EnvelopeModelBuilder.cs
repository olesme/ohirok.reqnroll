using Io.Cucumber.Messages.Types;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.Reporting.Config;
using Ohirok.Reporting.Model;

namespace Ohirok.Reporting.Parsing
{
    /// <summary>
    /// Builds a <see cref="ReportRun"/> tree from a buffered run of Cucumber Message envelopes. One linear
    /// batch pass (called from the formatter's Dispose, not incrementally): a whole run is needed anyway to
    /// group retry attempts and reruns, so the envelopes are buffered rather than streamed.
    /// </summary>
    public static class EnvelopeModelBuilder
    {
        public static ReportRun Build(IReadOnlyList<Envelope> envelopes)
        {
            var gherkinDocsByUri = new Dictionary<string, GherkinDocument>();
            var picklesById = new Dictionary<string, Pickle>();
            var testCasesById = new Dictionary<string, TestCase>();
            var testCaseStartedById = new Dictionary<string, TestCaseStarted>();
            var testCaseFinishedByStartedId = new Dictionary<string, TestCaseFinished>();
            var stepFinishedByKey = new Dictionary<string, TestStepFinished>();
            var attachmentsByStartedId = new Dictionary<string, List<Attachment>>();
            var hooksById = new Dictionary<string, Hook>();
            var stepDefinitionsById = new Dictionary<string, StepDefinition>();

            // Reqnroll's Cucumber Messages emission reuses the FIRST static TestStep.Id for every runtime firing
            // of a per-step hook (BeforeStep/AfterStep recur once per real step, unlike scenario/test-run hooks
            // which fire once) - so a hook occurrence can't be resolved by looking up its own static id once the
            // hook has fired more than once in the same attempt. Pairing each TestStepStarted/Finished with the
            // attachments emitted strictly between them, in original envelope order, recovers correct per-firing
            // status/attachments regardless of how many static ids alias onto the same runtime-reported id.
            var pendingHookAttachmentsByKey = new Dictionary<string, List<Attachment>>();
            var hookOccurrencesByKey = new Dictionary<string, Queue<(TestStepFinished Finished, List<Attachment> Attachments)>>();

            Timestamp? testRunStartedTimestamp = null;
            Timestamp? testRunFinishedTimestamp = null;

            foreach (var env in envelopes)
            {
                if (env.GherkinDocument != null)
                    gherkinDocsByUri[env.GherkinDocument.Uri ?? ""] = env.GherkinDocument;
                else if (env.Pickle != null)
                    picklesById[env.Pickle.Id] = env.Pickle;
                else if (env.TestCase != null)
                    testCasesById[env.TestCase.Id] = env.TestCase;
                else if (env.TestCaseStarted != null)
                    testCaseStartedById[env.TestCaseStarted.Id] = env.TestCaseStarted;
                else if (env.TestCaseFinished != null)
                    testCaseFinishedByStartedId[env.TestCaseFinished.TestCaseStartedId] = env.TestCaseFinished;
                else if (env.TestStepStarted != null)
                {
                    pendingHookAttachmentsByKey[StepKey(env.TestStepStarted.TestCaseStartedId, env.TestStepStarted.TestStepId)] = [];
                }
                else if (env.TestStepFinished != null)
                {
                    var key = StepKey(env.TestStepFinished.TestCaseStartedId, env.TestStepFinished.TestStepId);
                    stepFinishedByKey[key] = env.TestStepFinished;

                    var occurrenceAttachments = pendingHookAttachmentsByKey.TryGetValue(key, out var pendingList) ? pendingList : [];
                    if (!hookOccurrencesByKey.TryGetValue(key, out var queue))
                        hookOccurrencesByKey[key] = queue = new Queue<(TestStepFinished, List<Attachment>)>();
                    queue.Enqueue((env.TestStepFinished, occurrenceAttachments));
                    pendingHookAttachmentsByKey.Remove(key);
                }
                else if (env.Attachment?.TestCaseStartedId != null)
                {
                    if (!attachmentsByStartedId.TryGetValue(env.Attachment.TestCaseStartedId, out var list))
                        attachmentsByStartedId[env.Attachment.TestCaseStartedId] = list = [];
                    list.Add(env.Attachment);

                    if (env.Attachment.TestStepId != null)
                    {
                        var stepKey = StepKey(env.Attachment.TestCaseStartedId, env.Attachment.TestStepId);
                        if (pendingHookAttachmentsByKey.TryGetValue(stepKey, out var pendingForStep))
                            pendingForStep.Add(env.Attachment);
                    }
                }
                else if (env.Hook != null)
                    hooksById[env.Hook.Id] = env.Hook;
                else if (env.StepDefinition != null)
                    stepDefinitionsById[env.StepDefinition.Id] = env.StepDefinition;
                else if (env.TestRunStarted != null)
                    testRunStartedTimestamp = env.TestRunStarted.Timestamp;
                else if (env.TestRunFinished != null)
                    testRunFinishedTimestamp = env.TestRunFinished.Timestamp;
            }

            var attemptsByTestCaseId = RetryAttemptGrouper.GroupByTestCaseId(testCaseStartedById, testCaseFinishedByStartedId);

            var stepsById = new Dictionary<string, Step>();
            var scenarioByAstId = new Dictionary<string, ReportScenario>();
            var root = new ReportFolder { Name = "" };
            var issuesByStartedId = new Dictionary<string, List<ReportIssueSummary>>();
            var hashtagsByStepId = new Dictionary<string, string[]>();
            var commentsByAnchorId = new Dictionary<string, string>();

            List<ReportAttachment> BuildAttachments(IEnumerable<Attachment> atts, string testCaseStartedId)
            {
                var list = new List<ReportAttachment>();
                foreach (var a in atts)
                {
                    if (a.ContentEncoding != AttachmentContentEncoding.BASE64 && a.MediaType == "text/x.cucumber.log+plain" && a.Body != null)
                    {
                        var scan = new IssueMarkerParser.ScanResult();
                        var cleaned = IssueMarkerParser.Scan(a.Body, scan);

                        if (scan.Issues.Count > 0)
                        {
                            if (!issuesByStartedId.TryGetValue(testCaseStartedId, out var bucket))
                                issuesByStartedId[testCaseStartedId] = bucket = [];
                            bucket.AddRange(scan.Issues);
                        }

                        if (cleaned == null) continue;

                        list.Add(new ReportAttachment
                        {
                            MediaType = a.MediaType,
                            Body = cleaned,
                            IsBase64Encoded = false,
                            FileName = a.FileName,
                            Url = a.Url
                        });
                        continue;
                    }

                    list.Add(ToReportAttachment(a));
                }
                return list;
            }

            void RegisterSteps(IEnumerable<Step> steps)
            {
                foreach (var step in steps)
                    stepsById[step.Id] = step;
            }

            List<string> Tags(IEnumerable<Tag> tags) => tags?.Select(t => t.Name).ToList() ?? [];

            List<ReportStep> StaticSteps(IEnumerable<Step> steps) => [.. steps
                .Select(s => new ReportStep
                {
                    Keyword = s.Keyword?.Trim() ?? "",
                    Text = s.Text ?? "",
                    Hashtags = hashtagsByStepId.TryGetValue(s.Id, out var h) ? [.. h] : [],
                    Comment = commentsByAnchorId.TryGetValue(s.Id, out var c) ? c : null,
                    Argument = ToArgument(s)
                })];

            void AddScenarioOrOutline(Scenario scenario, List<ReportScenario> scenarios, List<ReportScenarioOutline> outlines, string keyPrefix)
            {
                RegisterSteps(scenario.Steps);

                if (scenario.Examples == null || scenario.Examples.Count == 0)
                {
                    var rs = new ReportScenario
                    {
                        Name = scenario.Name,
                        ScenarioKey = keyPrefix + "::" + scenario.Name,
                        Description = scenario.Description,
                        Tags = Tags(scenario.Tags),
                        Comment = commentsByAnchorId.TryGetValue(scenario.Id, out var scenarioComment) ? scenarioComment : null,
                        Attempts = { new ReportAttempt { Steps = StaticSteps(scenario.Steps) } }
                    };
                    scenarios.Add(rs);
                    scenarioByAstId[scenario.Id] = rs;
                    return;
                }

                var outline = new ReportScenarioOutline
                {
                    Name = scenario.Name,
                    Description = scenario.Description,
                    Tags = Tags(scenario.Tags),
                    Comment = commentsByAnchorId.TryGetValue(scenario.Id, out var outlineComment) ? outlineComment : null,
                    StepTemplates = StaticSteps(scenario.Steps)
                };

                foreach (var examples in scenario.Examples)
                {
                    var headers = examples.TableHeader?.Cells?.Select(c => c.Value ?? "").ToList() ?? [];
                    var exampleSet = new ReportExampleSet
                    {
                        Name = examples.Name,
                        Description = examples.Description,
                        Tags = Tags(examples.Tags),
                        Comment = commentsByAnchorId.TryGetValue(examples.Id, out var exampleSetComment) ? exampleSetComment : null,
                        Headers = headers
                    };

                    var rowIndex = 0;
                    foreach (var row in examples.TableBody ?? [])
                    {
                        var cells = row.Cells ?? [];
                        var argumentValues = new Dictionary<string, string>();
                        for (var cellIndex = 0; cellIndex < cells.Count; cellIndex++)
                        {
                            var header = cellIndex < headers.Count ? headers[cellIndex] : "Column" + (cellIndex + 1);
                            argumentValues[header] = cells[cellIndex].Value ?? "";
                        }

                        var rs = new ReportScenario
                        {
                            // Fallback if no matching Pickle is ever found below - overwritten with the instantiated
                            // text once the pickle-processing pass resolves rs.Name = pickle.Name.
                            Name = scenario.Name,
                            NameTemplate = scenario.Name,
                            ScenarioKey = keyPrefix + "::" + scenario.Name + "::" + examples.Name + "::" + rowIndex,
                            Comment = commentsByAnchorId.TryGetValue(row.Id, out var rowComment) ? rowComment : null,
                            ArgumentValues = argumentValues
                        };
                        exampleSet.Scenarios.Add(rs);
                        scenarioByAstId[row.Id] = rs;
                        rowIndex++;
                    }

                    outline.ExampleSets.Add(exampleSet);
                }

                outlines.Add(outline);
            }

            void AddFeatureToFolderTree(string uri, ReportFeature feature)
            {
                var normalized = (uri ?? "").Replace('\\', '/');
                var segments = normalized.Split('/').Where(s => s.Length > 0).ToArray();
                var dirs = segments.Length > 0 ? segments.Take(segments.Length - 1) : [];

                var current = root;
                foreach (var dir in dirs)
                {
                    var next = current.Subfolders.FirstOrDefault(f => f.Name == dir);
                    if (next == null)
                    {
                        next = new ReportFolder { Name = dir };
                        current.Subfolders.Add(next);
                    }
                    current = next;
                }

                current.Features.Add(feature);
            }

            foreach (var kvp in gherkinDocsByUri)
            {
                var uri = kvp.Key;
                var doc = kvp.Value;
                var feature = doc.Feature;
                if (feature == null) continue;

                foreach (var kv in StaticHashtagAssociator.Associate(doc))
                    hashtagsByStepId[kv.Key] = kv.Value;

                // Read from this document's own comment dict (not the shared commentsByAnchorId below) for the
                // Feature-level lookup specifically - FeatureAnchorId is the one sentinel key shared by every
                // document (Feature has no wire-format Id), so merging it into the cross-document dict and reading
                // it back out would leak one feature file's Feature-comment onto every subsequent feature file that
                // has none of its own. Every other anchor kind (Rule/Scenario/Examples/TableRow/Step) has a real,
                // globally-unique Id, so merging is safe for those.
                var docComments = StaticHashtagAssociator.AssociateComments(doc);
                foreach (var kv in docComments)
                    commentsByAnchorId[kv.Key] = kv.Value;

                var reportFeature = new ReportFeature
                {
                    Name = feature.Name,
                    Description = feature.Description,
                    Uri = uri,
                    Tags = Tags(feature.Tags),
                    Comment = docComments.TryGetValue(StaticHashtagAssociator.FeatureAnchorId, out var featureComment) ? featureComment : null
                };

                foreach (var child in feature.Children ?? [])
                {
                    if (child.Background != null)
                    {
                        RegisterSteps(child.Background.Steps);
                        reportFeature.Background = StaticSteps(child.Background.Steps);
                        reportFeature.BackgroundName = child.Background.Name;
                        reportFeature.BackgroundDescription = child.Background.Description;
                    }
                    else if (child.Scenario != null)
                        AddScenarioOrOutline(child.Scenario, reportFeature.Scenarios, reportFeature.ScenarioOutlines, uri);
                    else if (child.Rule != null)
                    {
                        var reportRule = new ReportRule
                        {
                            Name = child.Rule.Name,
                            Description = child.Rule.Description,
                            Tags = Tags(child.Rule.Tags),
                            Comment = commentsByAnchorId.TryGetValue(child.Rule.Id, out var ruleComment) ? ruleComment : null
                        };

                        foreach (var ruleChild in child.Rule.Children ?? [])
                        {
                            if (ruleChild.Background != null)
                            {
                                RegisterSteps(ruleChild.Background.Steps);
                                reportRule.Background = StaticSteps(ruleChild.Background.Steps);
                                reportRule.BackgroundName = ruleChild.Background.Name;
                                reportRule.BackgroundDescription = ruleChild.Background.Description;
                            }
                            else if (ruleChild.Scenario != null)
                                AddScenarioOrOutline(ruleChild.Scenario, reportRule.Scenarios, reportRule.ScenarioOutlines, uri + "::" + child.Rule.Name);
                        }

                        reportFeature.Rules.Add(reportRule);
                    }
                }

                AddFeatureToFolderTree(uri, reportFeature);
            }

            foreach (var pickle in picklesById.Values)
            {
                var rs = pickle.AstNodeIds?.Select(id => scenarioByAstId.TryGetValue(id, out var s) ? s : null).FirstOrDefault(s => s != null);
                if (rs == null) continue;

                // Outline example rows are constructed with the raw un-instantiated Scenario Outline title
                // (kept on NameTemplate); the Pickle carries the actual substituted text, which is what the
                // tree should display - plain (non-outline) scenarios have no placeholders, so this is a no-op there.
                if (pickle.Name != null) rs.Name = pickle.Name;
                rs.Tags = pickle.Tags?.Select(t => t.Name).ToList() ?? rs.Tags;

                var testCase = testCasesById.Values.FirstOrDefault(tc => tc.PickleId == pickle.Id);
                if (testCase == null) continue;

                if (!attemptsByTestCaseId.TryGetValue(testCase.Id, out var attemptPairs) || attemptPairs.Count == 0) continue;

                var realStepTestStepIds = new HashSet<string>(
                    (testCase.TestSteps ?? [])
                        .Where(ts => ts.PickleStepId != null)
                        .Select(ts => ts.Id));
                var hookTestStepIds = new HashSet<string>(
                    (testCase.TestSteps ?? [])
                        .Where(ts => ts.HookId != null)
                        .Select(ts => ts.Id));
                var pickleStepsById = (pickle.Steps ?? []).ToDictionary(ps => ps.Id);
                var canonicalHookTestStepIdByHookId = (testCase.TestSteps ?? [])
                    .Where(ts => ts.HookId != null)
                    .GroupBy(ts => ts.HookId)
                    .ToDictionary(g => g.Key, g => g.First().Id);

                var attempts = new List<ReportAttempt>();
                var scenarioIssues = new List<ReportIssueSummary>();

                foreach (var pair in attemptPairs)
                {
                    var started = pair.Started;

                    // Single ordered pass over TestCase.TestSteps - it already interleaves pickle-step and hook
                    // TestSteps in true execution order, so a BeforeStep hook TestStep is always seen immediately
                    // before the step it wraps and an AfterStep hook TestStep immediately after - which is what
                    // lets BeforeStep/AfterStep hooks attach to that specific ReportStep instead of only being
                    // grouped into the attempt-level Before/After hook buckets (old SpecFlow LivingDoc precedent:
                    // step/hook output nests inside the step).
                    var steps = new List<ReportStep>();
                    var hooks = new List<ReportHook>();
                    var pendingBeforeStepHooks = new List<ReportHook>();

                    foreach (var testStep in testCase.TestSteps ?? [])
                    {
                        if (testStep.HookId != null)
                        {
                            var hook = hooksById.TryGetValue(testStep.HookId, out var h) ? h : null;
                            var hookType = FormatHookType(hook?.Type);

                            var reportHook = new ReportHook
                            {
                                Type = hookType,
                                SourceReference = FormatSourceReference(hook?.SourceReference)
                            };

                            if (canonicalHookTestStepIdByHookId.TryGetValue(testStep.HookId, out var canonicalId)
                                && hookOccurrencesByKey.TryGetValue(StepKey(started.Id, canonicalId), out var occurrenceQueue)
                                && occurrenceQueue.Count > 0)
                            {
                                var (hookFinished, hookAttachments) = occurrenceQueue.Dequeue();
                                reportHook.Status = MapStatus(hookFinished.TestStepResult.Status);
                                reportHook.ErrorMessage = hookFinished.TestStepResult.Message;
                                reportHook.StackTrace = hookFinished.TestStepResult.Exception?.StackTrace;
                                reportHook.DurationMs = ToMilliseconds(hookFinished.TestStepResult.Duration);
                                reportHook.Attachments = BuildAttachments(hookAttachments, started.Id);
                            }

                            if (hookType == "BeforeStep")
                                pendingBeforeStepHooks.Add(reportHook);
                            else if (hookType == "AfterStep" && steps.Count > 0)
                                steps[steps.Count - 1].AfterHooks.Add(reportHook);
                            else
                                hooks.Add(reportHook);

                            continue;
                        }

                        // A step a plugin's generator added (bound to a step definition, but to no Gherkin step - e.g.
                        // SoftAssert's end-of-scenario flush) is a real step of the scenario: it can fail it.
                        var pickleStep = !string.IsNullOrEmpty(testStep.PickleStepId) && pickleStepsById.TryGetValue(testStep.PickleStepId, out var ps) ? ps : null;
                        var isGenerated = pickleStep == null && (testStep.StepDefinitionIds?.Count ?? 0) > 0;
                        if (pickleStep == null && !isGenerated)
                            continue;

                        var astStepId = pickleStep?.AstNodeIds?.FirstOrDefault(id => stepsById.ContainsKey(id));
                        var astStep = astStepId != null ? stepsById[astStepId] : null;

                        var stepDefinitionId = testStep.StepDefinitionIds?.FirstOrDefault();
                        var pattern = stepDefinitionId != null && stepDefinitionsById.TryGetValue(stepDefinitionId, out var stepDefinition)
                            ? stepDefinition.Pattern?.Source
                            : null;

                        var reportStep = new ReportStep
                        {
                            // "*" is Gherkin's keyword for a step without a Given/When/Then role.
                            Keyword = isGenerated ? "*" : astStep?.Keyword?.Trim() ?? "",
                            Text = isGenerated ? pattern ?? "" : pickleStep!.Text ?? "",
                            Hashtags = astStep != null && hashtagsByStepId.TryGetValue(astStep.Id, out var stepHashtags)
                                ? [.. stepHashtags]
                                : [],
                            Comment = astStep != null && commentsByAnchorId.TryGetValue(astStep.Id, out var stepComment)
                                ? stepComment
                                : null,
                            Argument = ToArgument(astStep),
                            Pattern = pattern
                        };

                        if (stepFinishedByKey.TryGetValue(StepKey(started.Id, testStep.Id), out var finished))
                        {
                            reportStep.Status = MapStatus(finished.TestStepResult.Status);
                            reportStep.DurationMs = ToMilliseconds(finished.TestStepResult.Duration);
                            reportStep.ErrorMessage = finished.TestStepResult.Message;
                            reportStep.StackTrace = finished.TestStepResult.Exception?.StackTrace;
                        }

                        if (attachmentsByStartedId.TryGetValue(started.Id, out var stepAttachments))
                        {
                            reportStep.Attachments = BuildAttachments(
                                stepAttachments.Where(a => a.TestStepId == testStep.Id), started.Id);
                        }

                        if (pendingBeforeStepHooks.Count > 0)
                        {
                            reportStep.BeforeHooks = pendingBeforeStepHooks;
                            pendingBeforeStepHooks = [];
                        }

                        steps.Add(reportStep);
                    }

                    // Attachments not tied to a real Gherkin step or a hook - i.e. purely scenario-scoped
                    // (TestStepId == null, such as SoftAssert's flush-time signal fired from a plain AfterScenario
                    // event handler rather than a hook) - are rolled up at the attempt level so their ReportIssue
                    // markers are still scanned and counted. Hook-attributed attachments are surfaced on their
                    // own ReportHook.Attachments above instead of being merged in here.
                    var attemptAttachments = attachmentsByStartedId.TryGetValue(started.Id, out var scenarioAttachments)
                        ? BuildAttachments(scenarioAttachments.Where(a => a.TestStepId == null || (!realStepTestStepIds.Contains(a.TestStepId) && !hookTestStepIds.Contains(a.TestStepId))), started.Id)
                        : [];

                    if (issuesByStartedId.TryGetValue(started.Id, out var attemptIssues))
                        scenarioIssues.AddRange(attemptIssues);

                    attempts.Add(new ReportAttempt
                    {
                        AttemptNumber = (int)started.Attempt + 1,
                        WillBeRetried = pair.Finished?.WillBeRetried ?? false,
                        Status = DeriveAttemptStatus(steps, hooks),
                        Steps = steps,
                        Attachments = attemptAttachments,
                        Hooks = hooks
                    });
                }

                rs.Attempts = attempts;
                rs.Issues = scenarioIssues;
                rs.Status = DeriveScenarioStatus(rs.FinalAttempt, attempts.Count, scenarioIssues);
                rs.Hashtags = [.. scenarioIssues.SelectMany(i => i.Hashtags).Distinct()];
                rs.FilterMatches = ReportFilterMatcher.Match(rs, ReportingConfig.Filters);
            }

            var startedAt = ToDateTimeOffset(testRunStartedTimestamp);
            var finishedAt = ToDateTimeOffset(testRunFinishedTimestamp);
            return new ReportRun
            {
                Root = root,
                FilterGroups = ReportingConfig.Filters,
                StartedAt = startedAt,
                FinishedAt = finishedAt,
                DurationMs = startedAt != null && finishedAt != null ? (finishedAt.Value - startedAt.Value).TotalMilliseconds : (double?)null
            };
        }

        private static string StepKey(string testCaseStartedId, string testStepId) => testCaseStartedId + "|" + testStepId;

        /// <summary>DocString/DataTable are static Gherkin syntax carried on the AST <see cref="Step"/> - never
        /// runtime-computed, so this reads straight off the already-parsed document rather than any execution data.</summary>
        private static ReportStepArgument? ToArgument(Step? astStep)
        {
            if (astStep?.DocString != null)
            {
                return new ReportStepArgument
                {
                    Kind = "docstring",
                    MediaType = astStep.DocString.MediaType,
                    Text = astStep.DocString.Content
                };
            }

            if (astStep?.DataTable != null)
            {
                return new ReportStepArgument
                {
                    Kind = "datatable",
                    Rows = [.. (astStep.DataTable.Rows ?? []).Select(row => (row.Cells ?? []).Select(c => c.Value ?? "").ToList())]
                };
            }

            return null;
        }

        private static string FormatHookType(HookType? type)
        {
            return type switch
            {
                HookType.BEFORE_TEST_RUN => "BeforeTestRun",
                HookType.AFTER_TEST_RUN => "AfterTestRun",
                HookType.BEFORE_TEST_CASE => "BeforeScenario",
                HookType.AFTER_TEST_CASE => "AfterScenario",
                HookType.BEFORE_TEST_STEP => "BeforeStep",
                HookType.AFTER_TEST_STEP => "AfterStep",
                _ => "Hook",
            };
        }

        private static string? FormatSourceReference(SourceReference? sourceRef)
        {
            if (sourceRef?.Uri == null) return null;
            return sourceRef.Location != null ? $"{sourceRef.Uri}:{sourceRef.Location.Line}" : sourceRef.Uri;
        }

        private static double ToMilliseconds(Duration? duration) =>
            duration == null ? 0 : duration.Seconds * 1000.0 + duration.Nanos / 1_000_000.0;

        private static DateTimeOffset? ToDateTimeOffset(Timestamp? timestamp) =>
            timestamp == null ? (DateTimeOffset?)null : DateTimeOffset.FromUnixTimeSeconds(timestamp.Seconds).AddTicks(timestamp.Nanos / 100);

        private static ReportAttachment ToReportAttachment(Attachment a) => new()
        {
            MediaType = a.MediaType,
            Body = a.Body,
            IsBase64Encoded = a.ContentEncoding == AttachmentContentEncoding.BASE64,
            FileName = a.FileName,
            Url = a.Url
        };

        private static ReportStatus MapStatus(TestStepResultStatus status)
        {
            return status switch
            {
                TestStepResultStatus.PASSED => ReportStatus.Passed,
                TestStepResultStatus.SKIPPED => ReportStatus.Skipped,
                TestStepResultStatus.PENDING => ReportStatus.Pending,
                TestStepResultStatus.UNDEFINED => ReportStatus.Undefined,
                TestStepResultStatus.AMBIGUOUS => ReportStatus.Ambiguous,
                TestStepResultStatus.FAILED => ReportStatus.Failed,
                _ => ReportStatus.Unknown,
            };
        }

        private static readonly ReportStatus[] TrueFailureStatusesWorstFirst =
        [
            ReportStatus.Failed, ReportStatus.Ambiguous, ReportStatus.Undefined, ReportStatus.Pending
        ];

        /// <summary>
        /// A single attempt's own literal wire status - true failure statuses win outright; a scenario where
        /// every step is Skipped/Unknown (never actually ran) reports as a real Skipped/Unknown; otherwise Passed.
        /// A failed hook fails the attempt like a failed step does: a broken cleanup, a failing Before hook (which
        /// leaves every step skipped) or a soft-assertion flush in an After hook must never look green.
        /// Never PassWithIssues - that escalation is a scenario-level concept, applied by <see cref="DeriveScenarioStatus"/>.
        /// </summary>
        private static ReportStatus DeriveAttemptStatus(List<ReportStep> steps, List<ReportHook> hooks)
        {
            var hookFailed = hooks.Concat(steps.SelectMany(s => s.BeforeHooks.Concat(s.AfterHooks)))
                .Any(h => h.Status == ReportStatus.Failed);
            if (hookFailed) return ReportStatus.Failed;

            var statuses = new HashSet<ReportStatus>(steps.Select(s => s.Status));

            foreach (var failureStatus in TrueFailureStatusesWorstFirst)
                if (statuses.Contains(failureStatus)) return failureStatus;

            if (steps.Count == 0 || steps.All(s => s.Status == ReportStatus.Skipped || s.Status == ReportStatus.Unknown))
                return statuses.Contains(ReportStatus.Skipped) ? ReportStatus.Skipped : ReportStatus.Unknown;

            return ReportStatus.Passed;
        }

        /// <summary>
        /// Scenario-level status is never a flat "worst step wins" (that would report a scenario as Skipped just
        /// because one #ignore'd step among otherwise-passing steps sets its own status to Skipped). The final attempt's true-failure/Skipped/Unknown status always passes straight
        /// through unchanged - a final attempt that Passed escalates to PassWithIssues when it was retried, has an
        /// associated <see cref="ReportIssueSummary"/>, or still has some Skipped steps of its own.
        /// </summary>
        internal static ReportStatus DeriveScenarioStatus(ReportAttempt? finalAttempt, int attemptCount, List<ReportIssueSummary> issues)
        {
            if (finalAttempt == null) return ReportStatus.NotRun;
            // A scenario-level @xfail that passed fails the test after its last step; the report must say failed too.
            if (issues.Any(i => i.Category == ReportIssueCategory.XFailPassed))
                return ReportStatus.Failed;
            // A failure the scenario's @xfail / @afail tag expected: the test passed, so the report must not say failed.
            if (finalAttempt.Status != ReportStatus.Passed && issues.Any(i => i.Category == ReportIssueCategory.ExpectedFailure))
                return ReportStatus.PassWithIssues;
            if (finalAttempt.Status != ReportStatus.Passed) return finalAttempt.Status;

            var hasIssue = attemptCount > 1 || issues.Count > 0 || finalAttempt.Steps.Any(s => s.Status == ReportStatus.Skipped);
            return hasIssue ? ReportStatus.PassWithIssues : ReportStatus.Passed;
        }
    }
}
