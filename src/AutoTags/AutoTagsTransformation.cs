using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Gherkin.Ast;
using Reqnroll.Parser;

namespace Ohirok.AutoTags
{
    /// <summary>
    /// Adds tags to scenarios by their steps, before anything is generated, and gives steps hashtags by their text: the
    /// test code (runner categories included), the tags Reqnroll's hooks are scoped by and the Cucumber messages see the
    /// tags as if they were written in the file, and the plugins reading step hashtags see the hashtags.
    /// <para>
    /// A scenario gets a configured tag when one of its own steps matches one of the tag's patterns, or when a step of a
    /// Background that runs before it does (the feature's, and its rule's). A tag the scenario, its rule or its feature
    /// already has is not added again. A step gets a configured hashtag when its own text matches one of the hashtag's
    /// patterns (through the SDK's <c>IStepHashtagSource</c>); hashtags written above the step come after it, so they win
    /// for plugins that use the last value.
    /// Patterns are regular expressions matched against the step text without the keyword, ignoring case.
    /// </para>
    /// </summary>
    public sealed class AutoTagsTransformation
    {
        readonly Rule[] _tags;
        readonly Rule[] _hashtags;

        // What each step text gives. Suites reuse the same step texts across many scenarios and feature files, and one
        // transformation serves a whole project, so most texts are matched against the patterns only once.
        readonly ConcurrentDictionary<string, string[]> _tagsByStepText = new(StringComparer.Ordinal);
        readonly ConcurrentDictionary<string, string[]> _hashtagsByStepText = new(StringComparer.Ordinal);

        sealed class Rule(string name, Regex[] patterns)
        {
            public string Name { get; } = name;
            public bool Matches(string text) => patterns.Any(p => p.IsMatch(text));
        }

        public AutoTagsTransformation(IDictionary<string, string[]>? tags, IDictionary<string, string[]>? hashtags = null)
        {
            _tags = Rules(tags, '@');
            _hashtags = Rules(hashtags, '#');
        }

        static Rule[] Rules(IDictionary<string, string[]>? config, char prefix) =>
            (config ?? new Dictionary<string, string[]>())
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key) && kv.Value is { Length: > 0 })
                .Select(kv =>
                {
                    var name = prefix + kv.Key.Trim().TrimStart(prefix);
                    return new Rule(name, kv.Value.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Compile(name, p)).ToArray());
                })
                .ToArray();

        static Regex Compile(string name, string pattern)
        {
            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException(
                    $"reqnroll.json: autoTags pattern '{pattern}' for {name} is not a valid regular expression: {ex.Message}", ex);
            }
        }

        public ReqnrollDocument Transform(ReqnrollDocument document)
        {
            var feature = document.ReqnrollFeature;
            if (_tags.Length == 0 || feature == null) return document;

            var transformedFeature = _tags.Length == 0 ? feature : AddTags(feature);
            return ReferenceEquals(transformedFeature, feature)
                ? document
                : new ReqnrollDocument(transformedFeature, document.Comments, document.DocumentLocation);
        }

        ReqnrollFeature AddTags(ReqnrollFeature feature)
        {
            var featureTags = new HashSet<string>(Names(feature.Tags), StringComparer.OrdinalIgnoreCase);
            var fromFeatureBackground = TagsOf(feature.Children.OfType<Background>().SelectMany(b => b.Steps));

            var changed = false;
            var children = new List<IHasLocation>();
            foreach (var child in feature.Children)
            {
                switch (child)
                {
                    case Scenario scenario:
                        children.Add(AddTags(scenario, fromFeatureBackground, featureTags, ref changed));
                        break;
                    case Gherkin.Ast.Rule rule:
                        var inherited = new HashSet<string>(featureTags.Concat(Names(rule.Tags)), StringComparer.OrdinalIgnoreCase);
                        var fromBackgrounds = fromFeatureBackground
                            .Concat(TagsOf(rule.Children.OfType<Background>().SelectMany(b => b.Steps)))
                            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        var ruleChanged = false;
                        var ruleChildren = rule.Children
                            .Select(c => c is Scenario s ? AddTags(s, fromBackgrounds, inherited, ref ruleChanged) : c)
                            .ToArray();
                        if (ruleChanged)
                        {
                            changed = true;
                            children.Add(new Gherkin.Ast.Rule(rule.Tags?.ToArray() ?? [], rule.Location, rule.Keyword, rule.Name, rule.Description, ruleChildren));
                        }
                        else children.Add(rule);
                        break;
                    default:
                        children.Add(child);
                        break;
                }
            }

            return changed
                ? new ReqnrollFeature(feature.Tags?.ToArray() ?? [], feature.Location, feature.Language, feature.Keyword,
                    feature.Name, feature.Description, children)
                : feature;
        }

        Scenario AddTags(Scenario scenario, IEnumerable<string> fromBackgrounds, ISet<string> inherited, ref bool changed)
        {
            var own = scenario.Tags?.ToArray() ?? [];
            var present = new HashSet<string>(inherited.Concat(Names(own)), StringComparer.OrdinalIgnoreCase);
            var added = fromBackgrounds.Concat(TagsOf(scenario.Steps))
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

        /// <summary>The configured hashtags (without <c>#</c>) whose patterns match the step's text, in configuration order.</summary>
        public string[] HashtagsFor(Step step) =>
            _hashtags.Length == 0 ? [] : Match(_hashtags, _hashtagsByStepText, step.Text ?? "").Select(h => h.Substring(1)).ToArray();

        /// <summary>The configured tags whose patterns match at least one of the steps, in configuration order.</summary>
        string[] TagsOf(IEnumerable<Step>? steps)
        {
            var found = new HashSet<string>((steps ?? []).SelectMany(s => Match(_tags, _tagsByStepText, s.Text ?? "")));
            return found.Count == 0 ? [] : _tags.Select(r => r.Name).Where(found.Contains).ToArray();
        }

        static string[] Match(Rule[] rules, ConcurrentDictionary<string, string[]> cache, string stepText) =>
            cache.GetOrAdd(stepText, text => rules.Where(r => r.Matches(text)).Select(r => r.Name).ToArray());

        static IEnumerable<string> Names(IEnumerable<Tag>? tags) => (tags ?? []).Select(t => t.Name);
    }
}
