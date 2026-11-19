using Io.Cucumber.Messages.Types;

namespace Ohirok.Reporting.Parsing
{
    /// <summary>
    /// Associates each step with the "#hashtag" comments that immediately precede it in the .feature file, purely
    /// by static parsing of the already-received <see cref="GherkinDocument.Comments"/> field - the same wire-format
    /// data every Cucumber Messages envelope stream carries unconditionally. This mirrors the generation-time
    /// convention (SDK's HashtagCache/HashtagExtractor, applied to Gherkin.Ast) but reimplemented against the
    /// wire-format AST so the reporting formatter never needs to round-trip hashtag identity through runtime output:
    /// it is fully knowable from the parsed document, so it is derived here instead.
    /// </summary>
    public static class StaticHashtagAssociator
    {
        /// <summary>Feature has no wire-format Id (confirmed via reflection on Cucumber.Messages.Types.Feature) - every
        /// other commentable anchor (Rule/Background/Scenario/Examples/TableRow/Step) does, so this sentinel is the only
        /// synthetic key CollectCommentLines produces.</summary>
        public const string FeatureAnchorId = "$feature";

        public static Dictionary<string, string[]> Associate(GherkinDocument doc)
        {
            var result = new Dictionary<string, string[]>();
            foreach (var kv in CollectCommentLines(doc))
            {
                var hashtags = kv.Value.SelectMany(ExtractHashtags).ToList();
                if (hashtags.Count > 0) result[kv.Key] = [.. hashtags];
            }
            return result;
        }

        /// <summary>Full raw comment text (every consecutive comment line, joined with "\n") immediately preceding
        /// each step - the same lines <see cref="Associate"/> scans for #hashtag tokens, kept verbatim here so
        /// report.js can display the comment itself above the step, not just the tokens extracted from it.</summary>
        public static Dictionary<string, string> AssociateComments(GherkinDocument doc)
        {
            var result = new Dictionary<string, string>();
            foreach (var kv in CollectCommentLines(doc))
            {
                var text = string.Join("\n", kv.Value).Trim();
                if (text.Length > 0) result[kv.Key] = text;
            }
            return result;
        }

        /// <summary>Every commentable location in the document (Feature/Rule/Background/Scenario/Examples/TableRow/Step
        /// headers), sorted by line, so a single boundary pass can attach comments at every level uniformly: an
        /// anchor's comment is every comment line strictly between it and the immediately preceding anchor of any
        /// kind - the preceding anchor's own header line is the boundary, so nesting falls out for free (a Scenario
        /// right after a Background "claims" the lines below the Background's last step, not the Background itself).</summary>
        private static Dictionary<string, List<string>> CollectCommentLines(GherkinDocument doc)
        {
            var result = new Dictionary<string, List<string>>();
            if (doc.Feature == null || doc.Comments == null || doc.Comments.Count == 0) return result;

            var commentsByLine = new Dictionary<long, string>();
            foreach (var comment in doc.Comments)
                commentsByLine[(long)comment.Location.Line] = comment.Text ?? "";

            var anchors = new List<(string Id, long Line)> { (FeatureAnchorId, (long)doc.Feature.Location.Line) };

            void CollectSteps(IEnumerable<Step>? stepList)
            {
                foreach (var step in stepList ?? [])
                    anchors.Add((step.Id, (long)step.Location.Line));
            }

            void CollectScenario(Scenario scenario)
            {
                anchors.Add((scenario.Id, (long)scenario.Location.Line));
                CollectSteps(scenario.Steps);
                foreach (var examples in scenario.Examples ?? [])
                {
                    anchors.Add((examples.Id, (long)examples.Location.Line));
                    foreach (var row in examples.TableBody ?? [])
                        anchors.Add((row.Id, (long)row.Location.Line));
                }
            }

            foreach (var child in doc.Feature.Children ?? [])
            {
                if (child.Background != null)
                {
                    anchors.Add((child.Background.Id, (long)child.Background.Location.Line));
                    CollectSteps(child.Background.Steps);
                }
                else if (child.Scenario != null)
                    CollectScenario(child.Scenario);
                else if (child.Rule != null)
                {
                    anchors.Add((child.Rule.Id, (long)child.Rule.Location.Line));
                    foreach (var ruleChild in child.Rule.Children ?? [])
                    {
                        if (ruleChild.Background != null)
                        {
                            anchors.Add((ruleChild.Background.Id, (long)ruleChild.Background.Location.Line));
                            CollectSteps(ruleChild.Background.Steps);
                        }
                        else if (ruleChild.Scenario != null)
                            CollectScenario(ruleChild.Scenario);
                    }
                }
            }

            anchors.Sort((a, b) => a.Line.CompareTo(b.Line));

            for (var i = 0; i < anchors.Count; i++)
            {
                var (id, line) = anchors[i];
                var lowerBound = i > 0 ? anchors[i - 1].Line : 0;

                var lines = new List<string>();
                for (var l = lowerBound + 1; l < line; l++)
                {
                    if (commentsByLine.TryGetValue(l, out var text))
                        lines.Add(text.TrimStart());
                }

                if (lines.Count > 0) result[id] = lines;
            }

            return result;
        }

        /// <summary>Same "#token" scan as the SDK's generation-time HashtagExtractor, reimplemented here to avoid
        /// taking a dependency on that internal SDK type for a ~15-line algorithm.</summary>
        private static IEnumerable<string> ExtractHashtags(string text)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            int i = 0, len = text.Length;
            while (i < len)
            {
                if (text[i] == '#' && i + 1 < len && text[i + 1] != ' ' && text[i + 1] != '\t')
                {
                    int start = i + 1, end = start;
                    while (end < len && text[end] != ' ' && text[end] != '\t') end++;
                    if (end > start) result.Add(text.Substring(start, end - start));
                    i = end;
                }
                else i++;
            }
            return result;
        }
    }
}
