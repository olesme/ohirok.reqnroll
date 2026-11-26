using System.Globalization;
using System.Text;
using Ohirok.Reporting.Config;
using Ohirok.Reporting.Model;

namespace Ohirok.Reporting.Rendering
{
    /// <summary>
    /// Hand-rolled JSON writer for the report model (no JSON package dependency needed for this small, fixed
    /// shape). Escapes '/' as '\/' so the output is safe to embed inside a "&lt;script&gt;" block even if step
    /// text happens to contain a literal "&lt;/script&gt;".
    /// </summary>
    public static class ReportJsonPayloadBuilder
    {
        /// <summary>Root JSON shape is {"runs": [{name, startedAt, finishedAt, durationMs, root, filterGroups}, ...]} -
        /// always an array, even for the everyday single-run case. report.js's entry point reads payload.runs.</summary>
        public static string Build(ReportProject project) => Build(project, null);

        /// <summary>Also carries the generation time and the date display settings, so report.js formats every
        /// timestamp the same way (see reporting.timeFormat / reporting.locale).</summary>
        public static string Build(ReportProject project, DateTimeOffset? generatedAt)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            if (generatedAt.HasValue)
            {
                AppendJsonString(sb, "generatedAt");
                sb.Append(':');
                AppendJsonString(sb, generatedAt.Value.ToString("o", CultureInfo.InvariantCulture));
                sb.Append(',');
            }
            AppendJsonString(sb, "display");
            sb.Append(":{");
            AppendJsonString(sb, "timeFormat");
            sb.Append(':');
            AppendJsonString(sb, ReportingConfig.TimeFormat);
            sb.Append(',');
            AppendJsonString(sb, "locale");
            sb.Append(':');
            AppendJsonString(sb, ReportingConfig.Locale);
            sb.Append(',');
            AppendJsonString(sb, "expandAttachments");
            sb.Append(":[");
            var kinds = ReportingConfig.ExpandAttachments;
            for (var i = 0; i < kinds.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendJsonString(sb, kinds[i]?.Trim().ToLowerInvariant() ?? "");
            }
            sb.Append("]},");
            AppendJsonString(sb, "runs");
            sb.Append(":[");
            for (var i = 0; i < project.Runs.Count; i++)
            {
                if (i > 0) sb.Append(',');
                WriteRun(sb, project.Runs[i]);
            }
            sb.Append(']');
            sb.Append('}');
            return sb.ToString();
        }

        private static void WriteRun(StringBuilder sb, ReportRun run)
        {
            sb.Append('{');
            WriteProp(sb, "name", run.Name, first: true);
            WriteProp(sb, "startedAt", run.StartedAt?.ToString("o", CultureInfo.InvariantCulture));
            WriteProp(sb, "finishedAt", run.FinishedAt?.ToString("o", CultureInfo.InvariantCulture));
            WriteNullableDoubleProp(sb, "durationMs", run.DurationMs);
            WriteNullableIntProp(sb, "historyAnalysisWindow", run.HistoryAnalysisWindow);
            WriteNullableIntProp(sb, "historyDisplayWindow", run.HistoryDisplayWindow);
            sb.Append(',');
            AppendJsonString(sb, "root");
            sb.Append(':');
            WriteFolder(sb, run.Root);
            WriteArrayProp(sb, "filterGroups", run.FilterGroups, WriteFilterGroup);
            sb.Append('}');
        }

        private static void WriteFilterGroup(StringBuilder sb, ReportFilterGroup group)
        {
            sb.Append('{');
            WriteProp(sb, "name", group.Name, first: true);
            WriteProp(sb, "otherLabel", group.OtherLabel);
            WriteProp(sb, "otherBg", group.OtherBg);
            WriteProp(sb, "otherFg", group.OtherFg);
            WriteArrayProp(sb, "options", group.Options, WriteFilterOption);
            sb.Append('}');
        }

        private static void WriteFilterOption(StringBuilder sb, ReportFilterOption option)
        {
            sb.Append('{');
            WriteProp(sb, "label", option.Label, first: true);
            WriteProp(sb, "bg", option.Bg);
            WriteProp(sb, "fg", option.Fg);
            sb.Append('}');
        }

        private static void WriteFilterMatch(StringBuilder sb, ReportFilterMatch match)
        {
            sb.Append('{');
            WriteProp(sb, "group", match.Group, first: true);
            WriteProp(sb, "label", match.Label);
            WriteProp(sb, "bg", match.Bg);
            WriteProp(sb, "fg", match.Fg);
            sb.Append('}');
        }

        private static void WriteFolder(StringBuilder sb, ReportFolder folder)
        {
            sb.Append('{');
            WriteProp(sb, "name", folder.Name, first: true);
            WriteArrayProp(sb, "subfolders", folder.Subfolders, WriteFolder);
            WriteArrayProp(sb, "features", folder.Features, WriteFeature);
            sb.Append('}');
        }

        private static void WriteFeature(StringBuilder sb, ReportFeature feature)
        {
            sb.Append('{');
            WriteProp(sb, "name", feature.Name, first: true);
            WriteProp(sb, "description", feature.Description);
            WriteProp(sb, "uri", feature.Uri);
            WriteStringArrayProp(sb, "tags", feature.Tags);
            WriteProp(sb, "comment", feature.Comment);
            WriteProp(sb, "backgroundName", feature.BackgroundName);
            WriteProp(sb, "backgroundDescription", feature.BackgroundDescription);
            WriteArrayProp(sb, "background", feature.Background, WriteStep);
            WriteArrayProp(sb, "rules", feature.Rules, WriteRule);
            WriteArrayProp(sb, "scenarios", feature.Scenarios, WriteScenario);
            WriteArrayProp(sb, "scenarioOutlines", feature.ScenarioOutlines, WriteScenarioOutline);
            sb.Append('}');
        }

        private static void WriteRule(StringBuilder sb, ReportRule rule)
        {
            sb.Append('{');
            WriteProp(sb, "name", rule.Name, first: true);
            WriteProp(sb, "description", rule.Description);
            WriteStringArrayProp(sb, "tags", rule.Tags);
            WriteProp(sb, "comment", rule.Comment);
            WriteProp(sb, "backgroundName", rule.BackgroundName);
            WriteProp(sb, "backgroundDescription", rule.BackgroundDescription);
            WriteArrayProp(sb, "background", rule.Background, WriteStep);
            WriteArrayProp(sb, "scenarios", rule.Scenarios, WriteScenario);
            WriteArrayProp(sb, "scenarioOutlines", rule.ScenarioOutlines, WriteScenarioOutline);
            sb.Append('}');
        }

        private static void WriteScenarioOutline(StringBuilder sb, ReportScenarioOutline outline)
        {
            sb.Append('{');
            WriteProp(sb, "name", outline.Name, first: true);
            WriteProp(sb, "description", outline.Description);
            WriteStringArrayProp(sb, "tags", outline.Tags);
            WriteProp(sb, "comment", outline.Comment);
            WriteArrayProp(sb, "stepTemplates", outline.StepTemplates, WriteStep);
            WriteArrayProp(sb, "exampleSets", outline.ExampleSets, WriteExampleSet);
            sb.Append('}');
        }

        private static void WriteExampleSet(StringBuilder sb, ReportExampleSet set)
        {
            sb.Append('{');
            WriteProp(sb, "name", set.Name, first: true);
            WriteProp(sb, "description", set.Description);
            WriteStringArrayProp(sb, "tags", set.Tags);
            WriteProp(sb, "comment", set.Comment);
            WriteStringArrayProp(sb, "headers", set.Headers);
            WriteArrayProp(sb, "scenarios", set.Scenarios, WriteScenario);
            sb.Append('}');
        }

        private static void WriteScenario(StringBuilder sb, ReportScenario scenario)
        {
            sb.Append('{');
            WriteProp(sb, "name", scenario.Name, first: true);
            WriteProp(sb, "nameTemplate", scenario.NameTemplate);
            WriteStringDictionaryPropOrNull(sb, "argumentValues", scenario.ArgumentValues);
            WriteProp(sb, "description", scenario.Description);
            WriteProp(sb, "comment", scenario.Comment);
            WriteStringArrayProp(sb, "tags", scenario.Tags);
            WriteStringArrayProp(sb, "hashtags", scenario.Hashtags);
            WriteProp(sb, "status", scenario.Status.ToString());
            WriteArrayPropOrNull(sb, "recentRuns", scenario.RecentRuns, WriteHistoryRun);
            WriteArrayProp(sb, "steps", scenario.Steps, WriteStep);
            WriteArrayProp(sb, "attachments", scenario.Attachments, WriteAttachment);
            WriteArrayProp(sb, "hooks", scenario.Hooks, WriteHook);
            WriteArrayProp(sb, "issues", scenario.Issues, WriteIssue);
            WriteArrayProp(sb, "attempts", scenario.Attempts, WriteAttempt);
            WriteArrayProp(sb, "filterMatches", scenario.FilterMatches, WriteFilterMatch);
            sb.Append('}');
        }

        private static void WriteAttempt(StringBuilder sb, ReportAttempt attempt)
        {
            sb.Append('{');
            WriteIntProp(sb, "attemptNumber", attempt.AttemptNumber, first: true);
            WriteBoolProp(sb, "willBeRetried", attempt.WillBeRetried);
            if (attempt.Rerun > 0) WriteIntProp(sb, "rerun", attempt.Rerun);
            WriteProp(sb, "status", attempt.Status.ToString());
            WriteArrayProp(sb, "steps", attempt.Steps, WriteStep);
            WriteArrayProp(sb, "attachments", attempt.Attachments, WriteAttachment);
            WriteArrayProp(sb, "hooks", attempt.Hooks, WriteHook);
            sb.Append('}');
        }

        private static void WriteHook(StringBuilder sb, ReportHook hook)
        {
            sb.Append('{');
            WriteProp(sb, "type", hook.Type, first: true);
            WriteProp(sb, "sourceReference", hook.SourceReference);
            WriteProp(sb, "status", hook.Status.ToString());
            WriteDoubleProp(sb, "durationMs", hook.DurationMs);
            WriteProp(sb, "errorMessage", hook.ErrorMessage);
            WriteProp(sb, "stackTrace", hook.StackTrace);
            WriteArrayProp(sb, "attachments", hook.Attachments, WriteAttachment);
            sb.Append('}');
        }

        private static void WriteIssue(StringBuilder sb, ReportIssueSummary issue)
        {
            sb.Append('{');
            WriteProp(sb, "sourcePlugin", issue.SourcePlugin, first: true);
            WriteProp(sb, "category", issue.Category.ToString());
            WriteProp(sb, "severity", issue.Severity);
            WriteProp(sb, "message", issue.Message);
            WriteStringArrayProp(sb, "tags", issue.Tags);
            WriteStringArrayProp(sb, "hashtags", issue.Hashtags);
            sb.Append('}');
        }

        private static void WriteStep(StringBuilder sb, ReportStep step)
        {
            sb.Append('{');
            WriteProp(sb, "keyword", step.Keyword, first: true);
            WriteProp(sb, "text", step.Text);
            WriteProp(sb, "status", step.Status.ToString());
            WriteDoubleProp(sb, "durationMs", step.DurationMs);
            WriteProp(sb, "errorMessage", step.ErrorMessage);
            WriteProp(sb, "stackTrace", step.StackTrace);
            WriteProp(sb, "pattern", step.Pattern);
            WriteProp(sb, "comment", step.Comment);
            WriteStringArrayProp(sb, "hashtags", step.Hashtags);
            WriteArrayProp(sb, "attachments", step.Attachments, WriteAttachment);
            WriteObjectProp(sb, "argument", step.Argument, WriteArgument);
            WriteArrayProp(sb, "beforeHooks", step.BeforeHooks, WriteHook);
            WriteArrayProp(sb, "afterHooks", step.AfterHooks, WriteHook);
            sb.Append('}');
        }

        private static void WriteArgument(StringBuilder sb, ReportStepArgument arg)
        {
            sb.Append('{');
            WriteProp(sb, "kind", arg.Kind, first: true);
            WriteProp(sb, "mediaType", arg.MediaType);
            WriteProp(sb, "text", arg.Text);
            WriteRowsProp(sb, "rows", arg.Rows);
            sb.Append('}');
        }

        private static void WriteHistoryRun(StringBuilder sb, ReportHistoryRunSummary run)
        {
            sb.Append('{');
            WriteProp(sb, "runAt", run.RunAt.ToString("o", CultureInfo.InvariantCulture), first: true);
            WriteProp(sb, "outcome", run.Outcome);
            WriteProp(sb, "runId", run.RunId);
            WriteProp(sb, "runUrl", run.RunUrl);
            WriteRawJsonProp(sb, "snapshot", run.SnapshotJson);
            sb.Append('}');
        }

        private static void WriteRawJsonProp(StringBuilder sb, string name, string? rawJson)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (string.IsNullOrWhiteSpace(rawJson)) sb.Append("null");
            else sb.Append(rawJson);
        }

        private static void WriteAttachment(StringBuilder sb, ReportAttachment attachment)
        {
            sb.Append('{');
            WriteProp(sb, "mediaType", attachment.MediaType, first: true);
            WriteProp(sb, "body", attachment.Body);
            WriteBoolProp(sb, "isBase64Encoded", attachment.IsBase64Encoded);
            WriteProp(sb, "fileName", attachment.FileName);
            WriteProp(sb, "url", attachment.Url);
            sb.Append('}');
        }

        private static void WriteProp(StringBuilder sb, string name, string? value, bool first = false)
        {
            if (!first) sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (value == null) sb.Append("null");
            else AppendJsonString(sb, value);
        }

        private static void WriteBoolProp(StringBuilder sb, string name, bool value)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':').Append(value ? "true" : "false");
        }

        private static void WriteIntProp(StringBuilder sb, string name, int value, bool first = false)
        {
            if (!first) sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':').Append(value);
        }

        private static void WriteDoubleProp(StringBuilder sb, string name, double value)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':').Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteNullableDoubleProp(StringBuilder sb, string name, double? value)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (value == null) sb.Append("null");
            else sb.Append(value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteNullableIntProp(StringBuilder sb, string name, int? value)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (value == null) sb.Append("null");
            else sb.Append(value.Value.ToString(CultureInfo.InvariantCulture));
        }

        private static void WriteObjectProp<T>(StringBuilder sb, string name, T? value, Action<StringBuilder, T> writeItem) where T : class
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (value == null) sb.Append("null");
            else writeItem(sb, value);
        }

        private static void WriteRowsProp(StringBuilder sb, string name, List<List<string>>? rows)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (rows == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('[');
            for (var i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append('[');
                var row = rows[i];
                for (var j = 0; j < row.Count; j++)
                {
                    if (j > 0) sb.Append(',');
                    AppendJsonString(sb, row[j]);
                }
                sb.Append(']');
            }
            sb.Append(']');
        }

        private static void WriteStringArrayProp(StringBuilder sb, string name, List<string> values)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(":[");
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                AppendJsonString(sb, values[i]);
            }
            sb.Append(']');
        }

        private static void WriteArrayProp<T>(StringBuilder sb, string name, List<T> values, Action<StringBuilder, T> writeItem)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(":[");
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                writeItem(sb, values[i]);
            }
            sb.Append(']');
        }

        private static void WriteStringDictionaryPropOrNull(StringBuilder sb, string name, Dictionary<string, string>? values)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (values == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('{');
            var first = true;
            foreach (var kvp in values)
            {
                if (!first) sb.Append(',');
                first = false;
                AppendJsonString(sb, kvp.Key);
                sb.Append(':');
                AppendJsonString(sb, kvp.Value);
            }
            sb.Append('}');
        }

        private static void WriteArrayPropOrNull<T>(StringBuilder sb, string name, List<T>? values, Action<StringBuilder, T> writeItem)
        {
            sb.Append(',');
            AppendJsonString(sb, name);
            sb.Append(':');
            if (values == null)
            {
                sb.Append("null");
                return;
            }

            sb.Append('[');
            for (var i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(',');
                writeItem(sb, values[i]);
            }
            sb.Append(']');
        }

        private static void AppendJsonString(StringBuilder sb, string value)
        {
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '/': sb.Append("\\/"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
