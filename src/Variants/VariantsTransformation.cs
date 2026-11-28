using Gherkin.Ast;
using Reqnroll.Parser;

namespace Ohirok.Variants
{
    /// <summary>
    /// Turns variant tags into Examples, so Reqnroll generates one test per variant through its own Scenario Outline
    /// support (row tests on every runner, one pickle per test).
    /// <para>
    /// For each configured key, the variants of a scenario come from the most specific level that declares the key:
    /// the scenario's tags, else its rule's, else the feature's. The tags are removed from those levels and put on
    /// the Examples blocks instead, one block per combination of values, so every generated test carries only its
    /// own variant tags. A Scenario Outline keeps its Examples; each block is repeated per combination with the
    /// variant columns added, except for a key the block already has a column for, whose values come from the block.
    /// </para>
    /// </summary>
    public sealed class VariantsTransformation(IEnumerable<string> keys)
    {
        readonly string[] _keys = keys?.Where(k => !string.IsNullOrWhiteSpace(k)).Select(k => k.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];

        public ReqnrollDocument Transform(ReqnrollDocument document)
        {
            if (_keys.Length == 0 || document.ReqnrollFeature == null) return document;

            var feature = document.ReqnrollFeature;
            var featureVariants = Extract(feature.Tags, out var featureTags);
            // Reqnroll's provider understands culture names (en-US) the way its parser does.
            var language = feature.Language ?? "en";
            var examplesKeyword = new ReqnrollGherkinDialectProvider(language).GetDialect(language, feature.Location).ExamplesKeywords
                .FirstOrDefault(k => k.Trim().Length > 0)?.Trim() ?? "Examples";

            var changed = featureVariants.Count > 0;
            var children = new List<IHasLocation>();
            foreach (var child in feature.Children)
            {
                switch (child)
                {
                    case Scenario scenario:
                        children.Add(Expand(scenario, featureVariants, examplesKeyword, ref changed));
                        break;
                    case Rule rule:
                        var ruleVariants = Extract(rule.Tags, out var ruleTags);
                        changed |= ruleVariants.Count > 0;
                        var inherited = Merge(featureVariants, ruleVariants);
                        var ruleChildren = rule.Children
                            .Select(c => c is Scenario s ? Expand(s, inherited, examplesKeyword, ref changed) : c)
                            .ToArray();
                        children.Add(new Rule(ruleTags, rule.Location, rule.Keyword, rule.Name, rule.Description, ruleChildren));
                        break;
                    default:
                        children.Add(child);
                        break;
                }
            }

            if (!changed) return document;

            var transformedFeature = new ReqnrollFeature(featureTags, feature.Location, feature.Language, feature.Keyword,
                feature.Name, feature.Description, children);
            return new ReqnrollDocument(transformedFeature, document.Comments, document.DocumentLocation);
        }

        IHasLocation Expand(Scenario scenario, IReadOnlyDictionary<string, List<Tag>> inherited, string examplesKeyword, ref bool changed)
        {
            var own = Extract(scenario.Tags, out var scenarioTags);
            var variants = Merge(inherited, own);
            if (variants.Count == 0) return scenario;
            changed = true;

            var existing = scenario.Examples?.ToArray() ?? [];
            var examples = existing.Length == 0
                ? Combinations(variants, _keys.Where(variants.ContainsKey).ToArray())
                    .Select(combo => new Examples(
                        [.. combo.Select(v => v.Tag)],
                        combo[0].Tag.Location,
                        examplesKeyword,
                        Describe(combo),
                        "",
                        new TableRow(combo[0].Tag.Location, [.. combo.Select(v => new TableCell(v.Tag.Location, v.Key))]),
                        [new TableRow(combo[0].Tag.Location, [.. combo.Select(v => new TableCell(v.Tag.Location, v.Value))])]))
                    .ToArray()
                : existing.SelectMany(block => ExpandExamples(block, variants)).ToArray();

            return new ScenarioOutline(scenarioTags, scenario.Location, scenario.Keyword, scenario.Name, scenario.Description,
                scenario.Steps, examples);
        }

        IEnumerable<Examples> ExpandExamples(Examples block, IReadOnlyDictionary<string, List<Tag>> variants)
        {
            // Reqnroll's parser rejects an Examples block without a table, so every block here has a header.
            var header = block.TableHeader.Cells.Select(c => c.Value).ToArray();
            // A key the block already has a column for takes its values from the block.
            var keys = _keys.Where(k => variants.ContainsKey(k) && !header.Contains(k, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (keys.Length == 0)
            {
                yield return block;
                yield break;
            }

            foreach (var combo in Combinations(variants, keys))
            {
                var location = combo[0].Tag.Location;
                var tableHeader = new TableRow(block.TableHeader.Location,
                    [.. block.TableHeader.Cells, .. combo.Select(v => new TableCell(location, v.Key))]);
                var body = (block.TableBody ?? []).Select(row => new TableRow(row.Location,
                    [.. row.Cells, .. combo.Select(v => new TableCell(location, v.Value))])).ToArray();
                var name = string.IsNullOrWhiteSpace(block.Name) ? Describe(combo) : $"{block.Name} ({Describe(combo)})";
                yield return new Examples([.. block.Tags ?? [], .. combo.Select(v => v.Tag)], block.Location, block.Keyword, name,
                    block.Description, tableHeader, body);
            }
        }

        /// <summary>The variant tags of one level, by configured key in the key's own spelling, and the other tags.</summary>
        Dictionary<string, List<Tag>> Extract(IEnumerable<Tag> tags, out Tag[] remaining)
        {
            var variants = new Dictionary<string, List<Tag>>(StringComparer.OrdinalIgnoreCase);
            var rest = new List<Tag>();
            foreach (var tag in tags ?? [])
            {
                var key = KeyOf(tag);
                if (key == null)
                {
                    rest.Add(tag);
                    continue;
                }
                if (Value(tag).Length == 0)
                    throw new SemanticParserException($"The variant tag {tag.Name} has no value: write {tag.Name}<value>.", tag.Location);
                if (!variants.TryGetValue(key, out var list)) variants[key] = list = [];
                if (!list.Any(t => string.Equals(Value(t), Value(tag), StringComparison.Ordinal))) list.Add(tag);
            }
            remaining = [.. rest];
            return variants;
        }

        string? KeyOf(Tag tag)
        {
            var name = tag.Name.TrimStart('@');
            var colon = name.IndexOf(':');
            if (colon <= 0) return null;
            var key = name.Substring(0, colon);
            return _keys.FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase));
        }

        static string Value(Tag tag)
        {
            var name = tag.Name;
            return name.Substring(name.IndexOf(':') + 1).Trim();
        }

        /// <summary>The more specific level replaces a key entirely; keys it does not declare are inherited.</summary>
        static Dictionary<string, List<Tag>> Merge(IReadOnlyDictionary<string, List<Tag>> outer, IReadOnlyDictionary<string, List<Tag>> inner)
        {
            var merged = new Dictionary<string, List<Tag>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in outer) merged[kv.Key] = kv.Value;
            foreach (var kv in inner) merged[kv.Key] = kv.Value;
            return merged;
        }

        /// <summary>All combinations of the values of <paramref name="keys"/>, in key order and declaration order.</summary>
        static IEnumerable<Variant[]> Combinations(IReadOnlyDictionary<string, List<Tag>> variants, string[] keys)
        {
            IEnumerable<Variant[]> combos = [[]];
            foreach (var key in keys)
            {
                var values = variants[key];
                combos = combos.SelectMany(c => values.Select(tag => (Variant[])[.. c, new Variant(key, Value(tag), tag)])).ToArray();
            }
            return combos;
        }

        static string Describe(IEnumerable<Variant> combo) => string.Join(", ", combo.Select(v => $"{v.Key}: {v.Value}"));

        readonly struct Variant(string key, string value, Tag tag)
        {
            public string Key { get; } = key;
            public string Value { get; } = value;
            public Tag Tag { get; } = tag;
        }
    }
}
