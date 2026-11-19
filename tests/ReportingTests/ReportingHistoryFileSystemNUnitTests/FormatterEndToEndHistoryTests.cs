using System.Text.RegularExpressions;
using Io.Cucumber.Messages.Types;
using NUnit.Framework;
using Ohirok.PluginChains.Common;
using Ohirok.Reporting.History.FileSystem.ReqnrollPlugin;
using Ohirok.Reporting.ReqnrollPlugin;
using Reqnroll.BoDi;
using Reqnroll.Formatters;
using Reqnroll.Formatters.Configuration;
using Reqnroll.Formatters.PubSub;
using Reqnroll.Formatters.RuntimeSupport;
using Reqnroll.Utils;

namespace Ohirok.Reporting.History.FileSystem.Tests
{
    /// <summary>
    /// Closes the gap left by <see cref="ScenarioHealthSteps"/>, which exercises
    /// <see cref="IReportHistoryStorage"/>/ReportHealthAnalyzer directly. This drives the real
    /// <see cref="OhirokReportFormatter"/> through two full, independent, sequential Cucumber Messages
    /// envelope streams (simulating two sequential CI runs sharing one ndjson history backend) via its
    /// actual public API (LaunchFormatter/PublishAsync/Dispose - no Reqnroll test-run host required),
    /// then asserts the resulting cross-run history in the second run's generated HTML. The server no
    /// longer ships a precomputed scenario.health - report.js derives it client-side from
    /// scenario.recentRuns, so the assertions here target that raw shipped array/RunId/outcome data, not
    /// a Health field.
    /// </summary>
    [TestFixture]
    public class FormatterEndToEndHistoryTests
    {
        private string _workDir = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ohirok-e2e-history-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_workDir);

            // Defaults (HistoryWindow=4/HistoryDisplayWindow=null->4) are enough for this test, but reset
            // first so no other test in this process (or a leftover reqnroll.json load) leaks a cached
            // "reportingHistory"/"reporting" section into this one - PluginConfigProvider's cache is static.
            PluginConfigProvider.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PluginConfigProvider.Reset();
            try { Directory.Delete(_workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        [Test]
        public async Task TwoSequentialRunsThroughRealFormatter_AccumulateHistoryInGeneratedReport()
        {
            const string featureUri = "e2e/History.feature";
            const string scenarioName = "A scenario tracked across CI runs";
            var historyPath = Path.Combine(_workDir, "history.ndjson");
            var storage = new FileSystemHistoryStorage(historyPath);
            await storage.InitializeAsync();

            await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run1"), featureUri, scenarioName, TestStepResultStatus.PASSED);

            // Ensures run2's RunAt is strictly later than run1's - GetRecentRunsAsync orders by RunAt, and
            // DateTimeOffset.UtcNow's resolution could otherwise tie two back-to-back calls.
            await Task.Delay(20);

            var run2Html = await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run2"), featureUri, scenarioName, TestStepResultStatus.FAILED);

            var json = ExtractEmbeddedJson(run2Html);

            Assert.That(json, Does.Not.Contain("\"health\""), "Server must not ship a precomputed scenario.health - report.js computes it client-side from recentRuns.");

            var recentRunsJson = ExtractRecentRunsArray(json, scenarioName);
            Assert.That(recentRunsJson, Is.Not.Null, "Expected the scenario to carry a recentRuns array after two runs against the same history backend.");

            var runIdMatches = Regex.Matches(recentRunsJson!, "\"runId\"\\s*:\\s*\"([^\"]+)\"");
            var outcomeMatches = Regex.Matches(recentRunsJson!, "\"outcome\"\\s*:\\s*\"([^\"]+)\"");

            Assert.That(runIdMatches.Count, Is.EqualTo(2), "Expected exactly 2 recentRuns entries (one per simulated CI run) sharing the same ScenarioKey.");
            Assert.That(outcomeMatches.Count, Is.EqualTo(2));

            var runIds = runIdMatches.Select(m => m.Groups[1].Value).ToList();
            var outcomes = outcomeMatches.Select(m => m.Groups[1].Value).ToList();

            Assert.That(runIds.Distinct().Count(), Is.EqualTo(2), "Each simulated run must carry its own distinct RunId.");
            Assert.That(outcomes[0], Is.EqualTo("Failed"), "Newest-first: run2 (Failed) must come before run1.");
            Assert.That(outcomes[1], Is.EqualTo("Passed"), "Oldest entry must be run1's outcome.");
        }

        /// <summary>Click-through archival (the RunUrl piece). With RunArchiveDirectory
        /// configured, each run's full HTML must be copied to "&lt;RunArchiveDirectory&gt;/&lt;RunId&gt;.html"
        /// and the embedded recentRuns entries must carry a non-null runUrl pointing at it.</summary>
        [Test]
        public async Task WithRunArchiveDirectoryConfigured_ArchivesEachRunAndShipsRunUrlInHistory()
        {
            const string featureUri = "e2e/History.feature";
            const string scenarioName = "A scenario archived across CI runs";
            var historyPath = Path.Combine(_workDir, "history.ndjson");
            var archiveDir = Path.Combine(_workDir, "archive");
            var storage = new FileSystemHistoryStorage(historyPath);
            await storage.InitializeAsync();

            var configPath = Path.Combine(_workDir, "reqnroll.json");
            await File.WriteAllTextAsync(configPath, $"{{ \"reportingHistory\": {{ \"runArchiveDirectory\": \"{archiveDir.Replace("\\", "\\\\")}\" }} }}");
            PluginConfigProvider.LoadFromFile(configPath);

            await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run1"), featureUri, scenarioName, TestStepResultStatus.PASSED);
            await Task.Delay(20);
            var run2Html = await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run2"), featureUri, scenarioName, TestStepResultStatus.FAILED);

            var json = ExtractEmbeddedJson(run2Html);
            var recentRunsJson = ExtractRecentRunsArray(json, scenarioName);
            Assert.That(recentRunsJson, Is.Not.Null);

            var runUrlMatches = Regex.Matches(recentRunsJson!, "\"runUrl\"\\s*:\\s*\"([^\"]+)\"");
            Assert.That(runUrlMatches.Count, Is.EqualTo(2), "Both archived runs must carry a non-null runUrl.");

            Assert.That(Directory.Exists(archiveDir), "Expected the configured archive directory to be created.");
            var archivedFiles = Directory.GetFiles(archiveDir, "*.html");
            Assert.That(archivedFiles.Length, Is.EqualTo(2), "Expected one archived HTML file per run.");

            foreach (var archivedFile in archivedFiles)
            {
                Assert.That(new FileInfo(archivedFile).Length, Is.GreaterThan(0), $"Archived file {archivedFile} must not be empty.");
            }
        }

        /// <summary>Option A: a base run and its rerun-of-the-same-scenario share one configured
        /// PipelineRunId, so their SaveRunAsync calls must upsert into ONE history row instead of two
        /// independent ones - this is the whole point of the feature (a base+rerun pair is one logical CI
        /// run, not two sibling history entries).</summary>
        [Test]
        public async Task TwoSequentialRunsWithSamePipelineRunId_CollapseIntoOneHistoryEntry()
        {
            const string featureUri = "e2e/History.feature";
            const string scenarioName = "A scenario rerun under the same PipelineRunId";
            var historyPath = Path.Combine(_workDir, "history.ndjson");
            var storage = new FileSystemHistoryStorage(historyPath);
            await storage.InitializeAsync();

            var configPath = Path.Combine(_workDir, "reqnroll.json");
            await File.WriteAllTextAsync(configPath, "{ \"reportingHistory\": { \"pipelineRunId\": \"ci-build-123\" } }");
            PluginConfigProvider.LoadFromFile(configPath);

            await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run1"), featureUri, scenarioName, TestStepResultStatus.PASSED);
            await Task.Delay(20);
            var run2Html = await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run2"), featureUri, scenarioName, TestStepResultStatus.FAILED);

            var json = ExtractEmbeddedJson(run2Html);
            var recentRunsJson = ExtractRecentRunsArray(json, scenarioName);
            Assert.That(recentRunsJson, Is.Not.Null);

            var runIdMatches = Regex.Matches(recentRunsJson!, "\"runId\"\\s*:\\s*\"([^\"]+)\"");
            var outcomeMatches = Regex.Matches(recentRunsJson!, "\"outcome\"\\s*:\\s*\"([^\"]+)\"");

            Assert.That(runIdMatches.Count, Is.EqualTo(1),
                "Both runs shared the same configured PipelineRunId - they must upsert into ONE history row, not two (Option A).");
            Assert.That(outcomeMatches.Count, Is.EqualTo(1));
            Assert.That(runIdMatches[0].Groups[1].Value, Is.EqualTo("ci-build-123"));
            Assert.That(outcomeMatches[0].Groups[1].Value, Is.EqualTo("Failed"),
                "The merged row must reflect the latest (rerun) outcome for this scenario.");
        }

        /// <summary>A CI pipeline passes its build id through the environment, so one reqnroll.json serves
        /// every build: "${VAR}" in pipelineRunId resolves the same way the report title does.</summary>
        [Test]
        public async Task PipelineRunId_ResolvesEnvironmentVariableReferences()
        {
            const string featureUri = "e2e/History.feature";
            const string scenarioName = "A scenario whose run id comes from the environment";
            var historyPath = Path.Combine(_workDir, "history.ndjson");
            var storage = new FileSystemHistoryStorage(historyPath);
            await storage.InitializeAsync();

            Environment.SetEnvironmentVariable("OHIROK_E2E_BUILD_ID", "build-777");
            try
            {
                var configPath = Path.Combine(_workDir, "reqnroll.json");
                await File.WriteAllTextAsync(configPath, "{ \"reportingHistory\": { \"pipelineRunId\": \"ci-${OHIROK_E2E_BUILD_ID}\" } }");
                PluginConfigProvider.LoadFromFile(configPath);

                var html = await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run1"), featureUri, scenarioName, TestStepResultStatus.PASSED);

                var recentRunsJson = ExtractRecentRunsArray(ExtractEmbeddedJson(html), scenarioName);
                Assert.That(recentRunsJson, Does.Contain("\"ci-build-777\""));
            }
            finally
            {
                Environment.SetEnvironmentVariable("OHIROK_E2E_BUILD_ID", null);
            }
        }

        /// <summary>A pipeline that publishes each run's report somewhere records that address, so the history links to
        /// it without any local archive; "${VAR}" references resolve.</summary>
        [Test]
        public async Task RunUrl_IsRecordedForTheHistoryLinks_WithEnvironmentVariablesResolved()
        {
            const string featureUri = "e2e/History.feature";
            const string scenarioName = "A scenario whose run report is published by the pipeline";
            var storage = new FileSystemHistoryStorage(Path.Combine(_workDir, "history.ndjson"));
            await storage.InitializeAsync();

            Environment.SetEnvironmentVariable("OHIROK_E2E_RUN", "4711");
            try
            {
                var configPath = Path.Combine(_workDir, "reqnroll.json");
                await File.WriteAllTextAsync(configPath,
                    "{ \"reportingHistory\": { \"runUrl\": \"https://ci.example/runs/${OHIROK_E2E_RUN}/report.html\" } }");
                PluginConfigProvider.LoadFromFile(configPath);

                var html = await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "run1"), featureUri, scenarioName, TestStepResultStatus.PASSED);

                var recentRunsJson = ExtractRecentRunsArray(ExtractEmbeddedJson(html), scenarioName);
                Assert.That(recentRunsJson!.Replace("\\/", "/"), Does.Contain("\"https://ci.example/runs/4711/report.html\""));
            }
            finally
            {
                Environment.SetEnvironmentVariable("OHIROK_E2E_RUN", null);
            }
        }

        /// <summary>Option A's correctness requirement: upserting by RunId is NOT a blind
        /// full-row replace. A rerun only re-executes a subset of scenarios, so the merge must leave
        /// scenarios absent from the rerun's snapshot untouched in storage, while updating only the ones
        /// the rerun actually covered. Asserted directly against storage (not the generated HTML), since
        /// the rerun process's own report tree only ever contains the scenarios it actually ran.</summary>
        [Test]
        public async Task RerunOfSubsetOfScenarios_WithSamePipelineRunId_MergesWithoutLosingUntouchedScenario()
        {
            const string featureUri = "e2e/History.feature";
            const string alphaName = "Alpha scenario untouched by the rerun";
            const string betaName = "Beta scenario fixed by the rerun";
            var historyPath = Path.Combine(_workDir, "history.ndjson");
            var storage = new FileSystemHistoryStorage(historyPath);
            await storage.InitializeAsync();

            var configPath = Path.Combine(_workDir, "reqnroll.json");
            await File.WriteAllTextAsync(configPath, "{ \"reportingHistory\": { \"pipelineRunId\": \"ci-build-456\" } }");
            PluginConfigProvider.LoadFromFile(configPath);

            // Base run: Alpha passes, Beta fails.
            await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "base"), featureUri,
                new List<(string Name, TestStepResultStatus Status)>
                {
                    (alphaName, TestStepResultStatus.PASSED),
                    (betaName, TestStepResultStatus.FAILED),
                });

            await Task.Delay(20);

            // Rerun-of-failures: only Beta re-executes, now passing. Alpha is NOT part of this run at all.
            await RunFormatterOnceAsync(storage, Path.Combine(_workDir, "rerun"), featureUri,
                new List<(string Name, TestStepResultStatus Status)> { (betaName, TestStepResultStatus.PASSED) });

            var runs = await storage.GetRecentRunsAsync(10);
            Assert.That(runs.Count, Is.EqualTo(1), "Base run + rerun shared one PipelineRunId - storage must hold exactly one merged row.");

            var mergedScenarios = runs[0].Scenarios;
            var alpha = mergedScenarios.SingleOrDefault(s => s.ScenarioName == alphaName);
            var beta = mergedScenarios.SingleOrDefault(s => s.ScenarioName == betaName);

            Assert.That(alpha, Is.Not.Null, "Alpha must still be present in the merged row even though the rerun never touched it.");
            Assert.That(alpha!.Outcome, Is.EqualTo(RunOutcome.Passed), "Alpha's original base-run outcome must survive the merge untouched.");

            Assert.That(beta, Is.Not.Null);
            Assert.That(beta!.Outcome, Is.EqualTo(RunOutcome.Passed), "Beta's merged outcome must reflect the rerun's (latest) result, not the base run's failure.");
        }

        /// <summary>Drives one simulated CI run end-to-end through the real formatter's public API and
        /// returns the generated report HTML's full text.</summary>
        private static Task<string> RunFormatterOnceAsync(
            IReportHistoryStorage storage, string outputDir, string featureUri, string scenarioName, TestStepResultStatus stepStatus) =>
            RunFormatterOnceAsync(storage, outputDir, featureUri, new List<(string Name, TestStepResultStatus Status)> { (scenarioName, stepStatus) });

        /// <summary>Overload for the upsert-merge tests, which need a single formatter run covering
        /// several independent scenarios (e.g. a base run's full scenario set, or a rerun covering only a
        /// subset of it).</summary>
        private static async Task<string> RunFormatterOnceAsync(
            IReportHistoryStorage storage, string outputDir, string featureUri, List<(string Name, TestStepResultStatus Status)> scenarios)
        {
            Directory.CreateDirectory(outputDir);
            var ndjsonPath = Path.Combine(outputDir, "ohirok-report.ndjson");

            var container = new ObjectContainer();
            container.RegisterInstanceAs(storage);

            var configProvider = new FakeFormattersConfigurationProvider(ndjsonPath);
            var logger = new FakeFormatterLog();
            var fileSystem = new FakeFileSystem();
            var broker = new FakeCucumberMessageBroker();

            var formatter = new OhirokReportFormatter(configProvider, logger, fileSystem, container);
            formatter.LaunchFormatter(broker);

            foreach (var envelope in BuildEnvelopes(featureUri, scenarios))
                await formatter.PublishAsync(envelope); // the TestRunFinished envelope auto-closes the formatter

            formatter.Dispose(); // triggers OnTargetFileStreamDisposing: builds the ReportRun, applies history, writes HTML

            var htmlPath = Path.ChangeExtension(ndjsonPath, ".html");
            Assert.That(File.Exists(htmlPath), $"Expected the formatter to have written {htmlPath}.");
            return await File.ReadAllTextAsync(htmlPath);
        }

        /// <summary>One scenario's full minimal lifecycle - just enough real Cucumber Messages for
        /// EnvelopeModelBuilder to produce one ReportScenario with a stable ScenarioKey and Status.</summary>
        private static List<Envelope> BuildEnvelopes(string featureUri, string scenarioName, TestStepResultStatus stepStatus) =>
            BuildEnvelopes(featureUri, new List<(string Name, TestStepResultStatus Status)> { (scenarioName, stepStatus) });

        /// <summary>Builds one feature containing N independent single-step scenarios - the
        /// upsert-merge tests need a base run's full scenario set and a rerun covering only a SUBSET of
        /// the same ScenarioKeys, within one formatter run each.</summary>
        private static List<Envelope> BuildEnvelopes(string featureUri, List<(string Name, TestStepResultStatus Status)> scenarios)
        {
            const string testRunStartedId = "trs-1";
            var zero = new Timestamp(0, 0);

            var featureChildren = new List<FeatureChild>();
            var runtimeEnvelopes = new List<Envelope>();
            var pickleEnvelopes = new List<Envelope>();
            var testCaseEnvelopes = new List<Envelope>();

            for (var i = 0; i < scenarios.Count; i++)
            {
                var n = i + 1;
                var scenarioId = $"scenario-{n}";
                var stepAstId = $"step-{n}";
                var pickleId = $"pickle-{n}";
                var pickleStepId = $"pickle-step-{n}";
                var testCaseId = $"tc-{n}";
                var testStepId = $"ts-{n}";
                var testCaseStartedId = $"tcs-{n}";
                var (scenarioName, stepStatus) = scenarios[i];

                var step = new Step(new Location(2, null), "Given ", StepKeywordType.CONTEXT, "a step", null, null, stepAstId);
                var scenario = new Scenario(new Location(1, null), new List<Tag>(), "Scenario", scenarioName, "",
                    new List<Step> { step }, new List<Examples>(), scenarioId);
                featureChildren.Add(new FeatureChild(null, null, scenario));

                var pickleStep = new PickleStep(null, new List<string> { stepAstId }, pickleStepId, PickleStepType.CONTEXT, "a step");
                var pickle = new Pickle(pickleId, featureUri, scenarioName, "en",
                    new List<PickleStep> { pickleStep }, new List<PickleTag>(), new List<string> { scenarioId });
                pickleEnvelopes.Add(Envelope.Create(pickle));

                var testStep = new TestStep(null, testStepId, pickleStepId, new List<string>(), new List<StepMatchArgumentsList>());
                var testCase = new TestCase(testCaseId, pickleId, new List<TestStep> { testStep }, testRunStartedId);
                testCaseEnvelopes.Add(Envelope.Create(testCase));

                var testCaseStarted = new TestCaseStarted(0, testCaseStartedId, testCaseId, null, zero);
                var testStepStarted = new TestStepStarted(testCaseStartedId, testStepId, zero);
                var testStepResult = new TestStepResult(new Duration(0, 0), null, stepStatus, null);
                var testStepFinished = new TestStepFinished(testCaseStartedId, testStepId, testStepResult, zero);
                var testCaseFinished = new TestCaseFinished(testCaseStartedId, zero, willBeRetried: false);

                runtimeEnvelopes.Add(Envelope.Create(testCaseStarted));
                runtimeEnvelopes.Add(Envelope.Create(testStepStarted));
                runtimeEnvelopes.Add(Envelope.Create(testStepFinished));
                runtimeEnvelopes.Add(Envelope.Create(testCaseFinished));
            }

            var feature = new Feature(new Location(1, null), new List<Tag>(), "en", "Feature", "E2E history feature", "", featureChildren);
            var gherkinDocument = new GherkinDocument(featureUri, feature, new List<Comment>());

            var testRunStarted = new TestRunStarted(zero, testRunStartedId);
            var overallSuccess = scenarios.All(s => s.Status == TestStepResultStatus.PASSED);
            var testRunFinished = new TestRunFinished("", overallSuccess, zero, null, testRunStartedId);

            var envelopes = new List<Envelope>
            {
                Envelope.Create(testRunStarted),
                Envelope.Create(gherkinDocument),
            };
            envelopes.AddRange(pickleEnvelopes);
            envelopes.AddRange(testCaseEnvelopes);
            envelopes.AddRange(runtimeEnvelopes);
            envelopes.Add(Envelope.Create(testRunFinished));

            return envelopes;
        }

        private static string ExtractEmbeddedJson(string html)
        {
            var match = Regex.Match(html, "<script type=\"application/json\" id=\"ohirok-report-data\">(.*?)</script>", RegexOptions.Singleline);
            Assert.That(match.Success, "Expected to find the embedded #ohirok-report-data JSON payload in the generated report HTML.");
            return match.Groups[1].Value;
        }

        /// <summary>No full JSON parser needed - just locates the scenario object by its unique "name" and
        /// slices out its own "recentRuns": [...] array by bracket counting (scenarios have no nested
        /// "recentRuns" of their own to ambiguously match against).</summary>
        private static string? ExtractRecentRunsArray(string json, string scenarioName)
        {
            var nameMarker = "\"name\":\"" + scenarioName + "\"";
            var nameIndex = json.IndexOf(nameMarker, StringComparison.Ordinal);
            Assert.That(nameIndex, Is.GreaterThanOrEqualTo(0), $"Expected to find scenario '{scenarioName}' in the embedded JSON.");

            var recentRunsKeyIndex = json.IndexOf("\"recentRuns\":", nameIndex, StringComparison.Ordinal);
            Assert.That(recentRunsKeyIndex, Is.GreaterThanOrEqualTo(0), "Expected a recentRuns property after the scenario's name.");

            var arrayStart = json.IndexOf('[', recentRunsKeyIndex);
            if (arrayStart < 0) return null; // recentRuns: null - no history yet

            var depth = 0;
            for (var i = arrayStart; i < json.Length; i++)
            {
                if (json[i] == '[') depth++;
                else if (json[i] == ']')
                {
                    depth--;
                    if (depth == 0) return json.Substring(arrayStart, i - arrayStart + 1);
                }
            }

            throw new InvalidOperationException("Unterminated recentRuns array in embedded JSON.");
        }

        private sealed class FakeFormattersConfigurationProvider : IFormattersConfigurationProvider
        {
            private readonly string _outputFilePath;

            public FakeFormattersConfigurationProvider(string outputFilePath) => _outputFilePath = outputFilePath;

            public bool Enabled => true;

            public IDictionary<string, object> GetFormatterConfigurationByName(string formatterName) =>
                new Dictionary<string, object> { ["outputFilePath"] = _outputFilePath };

            public string ResolveTemplatePlaceholders(string template) => template;
        }

        private sealed class FakeFormatterLog : IFormatterLog
        {
            public void WriteMessage(string message) { }
            public void DumpMessages() { }
        }

        private sealed class FakeFileSystem : IFileSystem
        {
            public bool FileExists(string path) => File.Exists(path);
            public bool DirectoryExists(string path) => Directory.Exists(path);
            public void CreateDirectory(string path) => Directory.CreateDirectory(path);
            public string[] GetDirectories(string path) => Directory.GetDirectories(path);
            public string[] GetDirectories(string path, string searchPattern) => Directory.GetDirectories(path, searchPattern);
            public string[] GetDirectories(string path, string searchPattern, SearchOption searchOptions) => Directory.GetDirectories(path, searchPattern, searchOptions);
            public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);
            public IEnumerable<string> EnumerateDirectories(string path, string searchPattern) => Directory.EnumerateDirectories(path, searchPattern);
            public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOptions) => Directory.EnumerateDirectories(path, searchPattern, searchOptions);
        }

        private sealed class FakeCucumberMessageBroker : ICucumberMessageBroker
        {
            public bool IsEnabled => true;
            public void Initialize() { }
            public void FormatterInitialized(ICucumberMessageFormatter formatterSink, bool enabled) { }
            public Task PublishAsync(Envelope message) => Task.CompletedTask;
        }
    }
}
