using System.CodeDom;
using Gherkin.Ast;
using Reqnroll.Parser;

namespace Ohirok.IgnoreReasons
{
    /// <summary>
    /// Tags that ignore scenarios with a reason. Reqnroll's own <c>@ignore</c> does the ignoring, so nothing about it
    /// changes per runner: the scenarios an alias covers get <c>@ignore</c> before generation, and the ignore attribute
    /// Reqnroll then generates for the test gets the alias's reason instead of its generic text.
    /// </summary>
    public sealed class IgnoreReasons
    {
        const string IgnoreTag = "@ignore";
        readonly (string Tag, string Reason)[] _aliases;

        public IgnoreReasons(IDictionary<string, string>? tags)
        {
            _aliases = (tags ?? new Dictionary<string, string>())
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Key))
                .Select(kv => ("@" + kv.Key.Trim().TrimStart('@'), (kv.Value ?? "").Trim()))
                .Where(a => !string.Equals(a.Item1, IgnoreTag, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }

        /// <summary>The reason of the most specific alias among the tags of a scenario, its rule and its feature
        /// (in that order), or null when none of them is an alias. An alias without a reason gives its own name.</summary>
        public string? ReasonFor(IEnumerable<Tag>? scenarioTags, IEnumerable<Tag>? ruleTags, IEnumerable<Tag>? featureTags)
        {
            if (_aliases.Length == 0) return null;
            foreach (var level in new[] { scenarioTags, ruleTags, featureTags })
                foreach (var tag in level ?? [])
                    foreach (var (alias, reason) in _aliases)
                        if (string.Equals(tag.Name, alias, StringComparison.OrdinalIgnoreCase))
                            return reason.Length > 0 ? reason : alias.Substring(1);
            return null;
        }

        /// <summary>Adds <c>@ignore</c> to every scenario an alias covers, on the scenario itself (also when the alias is on
        /// its rule or feature), so each test gets its own ignore attribute to carry the reason.</summary>
        public ReqnrollDocument Transform(ReqnrollDocument document)
        {
            var feature = document.ReqnrollFeature;
            if (_aliases.Length == 0 || feature == null) return document;

            var changed = false;
            var children = new List<IHasLocation>();
            foreach (var child in feature.Children)
            {
                switch (child)
                {
                    case Scenario scenario:
                        children.Add(Ignore(scenario, null, feature.Tags, ref changed));
                        break;
                    case Rule rule:
                        var ruleChanged = false;
                        var ruleChildren = rule.Children
                            .Select(c => c is Scenario s ? Ignore(s, rule.Tags, feature.Tags, ref ruleChanged) : c)
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

        Scenario Ignore(Scenario scenario, IEnumerable<Tag>? ruleTags, IEnumerable<Tag>? featureTags, ref bool changed)
        {
            var own = scenario.Tags?.ToArray() ?? [];
            if (ReasonFor(own, ruleTags, featureTags) == null) return scenario;
            if (own.Any(t => string.Equals(t.Name, IgnoreTag, StringComparison.OrdinalIgnoreCase))) return scenario;
            changed = true;

            Tag[] tags = [.. own, new Tag(scenario.Location, IgnoreTag)];
            var examples = scenario.Examples?.ToArray() ?? [];
            return scenario is ScenarioOutline
                ? new ScenarioOutline(tags, scenario.Location, scenario.Keyword, scenario.Name, scenario.Description, scenario.Steps, examples)
                : new Scenario(tags, scenario.Location, scenario.Keyword, scenario.Name, scenario.Description, scenario.Steps, examples);
        }

        /// <summary>
        /// Puts the reason on the ignore attribute Reqnroll generated for the test method: the message of NUnit's and
        /// MSTest's <c>[Ignore]</c> and TUnit's <c>[Skip]</c>, the <c>Skip</c> of xUnit's <c>[Fact]</c> / <c>[Theory]</c>.
        /// Returns whether such an attribute was found.
        /// </summary>
        public static bool SetReason(CodeMemberMethod method, string reason)
        {
            var found = false;
            foreach (CodeAttributeDeclaration attribute in method.CustomAttributes)
            {
                var arguments = attribute.Arguments.Cast<CodeAttributeArgument>().ToArray();
                if (attribute.Name.EndsWith(".IgnoreAttribute", StringComparison.Ordinal) ||
                    attribute.Name.EndsWith(".SkipAttribute", StringComparison.Ordinal))
                {
                    var message = arguments.FirstOrDefault(a => string.IsNullOrEmpty(a.Name));
                    if (message != null) message.Value = new CodePrimitiveExpression(reason);
                    else attribute.Arguments.Insert(0, new CodeAttributeArgument(new CodePrimitiveExpression(reason)));
                    found = true;
                }
                else if (arguments.FirstOrDefault(a => a.Name == "Skip") is { } skip)
                {
                    skip.Value = new CodePrimitiveExpression(reason);
                    found = true;
                }
            }
            return found;
        }
    }
}
