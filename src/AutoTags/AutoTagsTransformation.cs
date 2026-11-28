using System.Text.RegularExpressions;
using Gherkin.Ast;
using Reqnroll.Parser;

namespace Ohirok.AutoTags
{
    /// <summary>
    /// Adds tags to scenarios by their steps, before anything is generated: the test code (runner categories included),
    /// the tags Reqnroll's hooks are scoped by and the Cucumber messages all see the tag as if it were written in the file.
    /// <para>
    /// A scenario gets a configured tag when one of its own steps matches one of the tag's patterns, or when a step of a
    /// Background that runs before it does (the feature's, and its rule's). A tag the scenario, its rule or its feature
    /// already has is not added again. Patterns are regular expressions matched against the step text without the
    /// keyword, ignoring case.
    /// </para>
    /// </summary>
    public sealed class AutoTagsTransformation
    {
        readonly (string Tag, Regex[] Patterns)[] _rules;

        public AutoTagsTransformation(IDictionary<string, string[]>? tags)
        {
            _rules = (tags ?? new Dictionary<string, string[]>())
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is { Length: > 0 })
                .Select(kv =>
                {
                    var tag = "@" + kv.Key.Trim().TrimStart('@');
                    return (tag, kv.Value.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Compile(tag, p)).ToArray());
                })
                .Where(r => r.Item2.Length > 0)
                .ToArray();
        }

        static Regex Compile(string tag, string pattern)
        {
            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"reqnroll.json: autoTags pattern '{pattern}' for {tag} is not a valid regular expression: {ex.Message}", ex);
            }
        }

        public ReqnrollDocument Transform(ReqnrollDocument document)
        {
            var feature = document.ReqnrollFeature;
            if (_rules.Length == 0 || feature == null) return document;

            var featureTags = new HashSet<string>(Names(feature.Tags), StringComparer.OrdinalIgnoreCase);
            var fromFeatureBackground = Matches(feature.Children.OfType<Background>().SelectMany(b => b.Steps));

            var changed = false;
            var children = new List<IHasLocation>();
            foreach (var child in feature.Children)
            {
                switch (child)
                {
                    case Scenario scenario:
                        children.Add(AddTags(scenario, fromFeatureBackground, featureTags, ref changed));
                        break;
                    case Rule rule:
                        var inherited = new HashSet<string>(featureTags.Concat(Names(rule.Tags)), StringComparer.OrdinalIgnoreCase);
                        var fromBackgrounds = fromFeatureBackground
                            .Concat(Matches(rule.Children.OfType<Background>().SelectMany(b => b.Steps)))
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        var ruleChanged = false;
                        var ruleChildren = rule.Children
                            .Select(c => c is Scenario s ? AddTags(s, fromBackgrounds, inherited, ref ruleChanged) : c)
                            .ToArray();
                        if (ruleChanged)
                        {
                            changed = true;
                            children.Add(new Rule(rule.Tags?.ToArray() ?? [], rule.Location, rule.Keyword, rule.Name, rule.Description, ruleChildren));
                        }
                        else children.Add(rule);
                        break;
                    default:
                        children.Add(child);
                        break;
                }
            }

            if (!changed) return document;

            var transformedFeature = new ReqnrollFeature(feature.Tags?.ToArray() ?? [], feature.Location, feature.Language,
                feature.Keyword, feature.Name, feature.Description, children);
            return new ReqnrollDocument(transformedFeature, document.Comments, document.DocumentLocation);
        }

        Scenario AddTags(Scenario scenario, IEnumerable<string> fromBackgrounds, ISet<string> inherited, ref bool changed)
        {
            var own = scenario.Tags?.ToArray() ?? [];
            var present = new HashSet<string>(inherited.Concat(Names(own)), StringComparer.OrdinalIgnoreCase);
            var added = fromBackgrounds.Concat(Matches(scenario.Steps))
                .Where(present.Add)
                .Select(name => new Tag(scenario.Location, name))
                .ToArray();
            if (added.Length == 0) return scenario;
            changed = true;

            Tag[] tags = [.. own, .. added];
            var examples = scenario.Examples?.ToArray() ?? [];
            return scenario is ScenarioOutline
                ? new ScenarioOutline(tags, scenario.Location, scenario.Keyword, scenario.Name, scenario.Description, scenario.Steps, examples)
                : new Scenario(tags, scenario.Location, scenario.Keyword, scenario.Name, scenario.Description, scenario.Steps, examples);
        }

        /// <summary>The configured tags whose patterns match at least one of the steps, in configuration order.</summary>
        string[] Matches(IEnumerable<Step>? steps)
        {
            var texts = (steps ?? []).Select(s => s.Text ?? "").ToArray();
            if (texts.Length == 0) return [];
            return _rules.Where(r => texts.Any(t => r.Patterns.Any(p => p.IsMatch(t)))).Select(r => r.Tag).ToArray();
        }

        static IEnumerable<string> Names(IEnumerable<Tag>? tags) => (tags ?? []).Select(t => t.Name);
    }
}
