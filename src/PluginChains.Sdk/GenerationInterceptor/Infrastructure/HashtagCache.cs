using Gherkin.Ast;
using Reqnroll.Generator;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Infrastructure
{
    internal static class HashtagCache
    {
        private const string Key = "__InterceptorHashtags__";

        public static string[] GetForStep(TestClassGenerationContext ctx, int line)
        {
            var cache = GetOrBuild(ctx);
            return cache.TryGetValue(line, out var tags) ? tags : [];
        }

        private static Dictionary<int, string[]> GetOrBuild(TestClassGenerationContext ctx)
        {
            if (ctx.CustomData.TryGetValue(Key, out var obj))
                return (Dictionary<int, string[]>)obj;

            var cache = Build(ctx);
            ctx.CustomData[Key] = cache;
            return cache;
        }

        private static Dictionary<int, string[]> Build(TestClassGenerationContext ctx)
        {
            var cache = new Dictionary<int, string[]>();
            var doc = ctx.Document;
            if (doc?.Comments == null || doc.ReqnrollFeature == null) return cache;

            var comments = doc.Comments.ToDictionary(c => c.Location.Line, c => c.Text);
            var steps = CollectSteps(doc.ReqnrollFeature);
            steps.Sort((a, b) => a.line.CompareTo(b.line));

            for (int i = 0; i < steps.Count; i++)
            {
                int upperBound = steps[i].containerLine;

                // Find previous step in same container
                for (int j = i - 1; j >= 0; j--)
                {
                    if (steps[j].containerLine == steps[i].containerLine)
                    {
                        upperBound = steps[j].line;
                        break;
                    }
                }

                var tags = new List<string>();
                for (int ln = upperBound + 1; ln < steps[i].line; ln++)
                {
                    if (comments.TryGetValue(ln, out var text))
                        tags.AddRange(HashtagExtractor.Extract(text));
                }

                if (tags.Count > 0) cache[steps[i].line] = [.. tags];
            }
            return cache;
        }

        private static List<(int line, int containerLine)> CollectSteps(ReqnrollFeature feature)
        {
            var result = new List<(int, int)>();

            // Feature background
            var bg = feature.Children.OfType<Background>().FirstOrDefault();
            if (bg != null)
            {
                foreach (var step in bg.Steps)
                    result.Add((step.Location.Line, bg.Location.Line));
            }

            foreach (var child in feature.Children)
            {
                if (child is Scenario scenario)
                {
                    foreach (var step in scenario.Steps)
                        result.Add((step.Location.Line, scenario.Location.Line));
                }
                else if (child is Rule rule)
                {
                    // Rule background
                    var ruleBg = rule.Children.OfType<Background>().FirstOrDefault();
                    if (ruleBg != null)
                    {
                        foreach (var step in ruleBg.Steps)
                            result.Add((step.Location.Line, ruleBg.Location.Line));
                    }

                    // Rule scenarios
                    foreach (var ruleScenario in rule.Children.OfType<Scenario>())
                    {
                        foreach (var step in ruleScenario.Steps)
                            result.Add((step.Location.Line, ruleScenario.Location.Line));
                    }
                }
            }
            return result;
        }
    }
}
