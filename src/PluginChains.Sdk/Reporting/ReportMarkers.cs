using System.Text;

namespace Ohirok.PluginChains.Common.Reporting
{
    /// <summary>
    /// Marker convention used to smuggle structured content through <see cref="Reqnroll.IReqnrollOutputHelper.WriteLine(string)"/>
    /// as self-describing custom HTML tags (&lt;ohirok-issue&gt;/&lt;ohirok-embed&gt;), so a reporting
    /// formatter can recognize and parse them out of plain-text attachments. Any other HTML-based formatter (which never
    /// expects or escapes these) still degrades gracefully: an unknown custom element renders as an anonymous inline
    /// container around its readable text, unlike the old invisible-separator+JSON scheme it replaces.
    /// </summary>
    public static class ReportMarkers
    {
        public const string IssueTag = "ohirok-issue";
        public const string EmbedTag = "ohirok-embed";

        /// <summary>Message is HTML-escaped: it is plain human-readable text, not markup, and must not break the tag it rides in.</summary>
        public static string WrapIssue(string category, string source, string? severity, string[]? tags, string[]? hashtags, string message)
            => Wrap(IssueTag,
            [
                ("category", (string?)category),
                ("source", source),
                ("severity", severity),
                ("tags", Join(tags)),
                ("hashtags", Join(hashtags))
            ], EscapeText(message));

        /// <summary>
        /// <paramref name="innerContent"/> is written verbatim (not escaped) - callout/raw-html embeds are rendered as
        /// HTML client-side by design (matches the pre-existing "rendered verbatim, never HTML-encoded" contract).
        /// </summary>
        public static string WrapEmbed(string kind, IEnumerable<(string Name, string? Value)> attributes, string innerContent = "")
        {
            var all = new List<(string, string?)> { ("kind", kind) };
            all.AddRange(attributes);
            return Wrap(EmbedTag, all, innerContent);
        }

        private static string? Join(string[]? values)
            => values == null || values.Length == 0 ? null : string.Join(",", values);

        private static string Wrap(string tagName, IEnumerable<(string Name, string? Value)> attributes, string innerContent)
        {
            var sb = new StringBuilder("<").Append(tagName);
            foreach (var (name, value) in attributes)
            {
                if (string.IsNullOrEmpty(value)) continue;
                sb.Append(' ').Append(name).Append("=\"").Append(EscapeAttribute(value!)).Append('"');
            }
            sb.Append('>').Append(innerContent).Append("</").Append(tagName).Append('>');
            return sb.ToString();
        }

        private static string EscapeAttribute(string value)
            => EscapeText(value).Replace("\"", "&quot;");

        private static string EscapeText(string value)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
