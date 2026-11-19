using System.Text;
using System.Text.RegularExpressions;
using Ohirok.PluginChains.Common.Reporting;
using Ohirok.Reporting.Model;

namespace Ohirok.Reporting.Parsing
{
    /// <summary>
    /// Recovers structured <see cref="ReportIssue"/> summaries that <c>OhirokReportOutputExtensions.ReportIssue</c>
    /// smuggles through plain-text WriteLine attachments as <c>&lt;ohirok-issue&gt;</c> tags (see <see cref="ReportMarkers"/>).
    /// Only that tag is recognized and stripped here - <see cref="ReportMarkers.EmbedTag"/> tags are deliberately left
    /// untouched in the returned text so the client-side renderer (report.js) can recognize them directly from the
    /// attachment body without a server-side round-trip through this model.
    /// </summary>
    public static class IssueMarkerParser
    {
        private static readonly Regex IssueTagPattern = new Regex(
            $"<{ReportMarkers.IssueTag}(?<attrs>(?:\\s+[\\w-]+=\"[^\"]*\")*)\\s*>(?<message>.*?)</{ReportMarkers.IssueTag}>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex AttributePattern = new Regex(
            "([\\w-]+)=\"([^\"]*)\"", RegexOptions.Compiled);

        public sealed class ScanResult
        {
            public List<ReportIssueSummary> Issues { get; } = [];
        }

        /// <summary>
        /// Scans a single text attachment body for &lt;ohirok-issue&gt; tags, appending any found summaries into
        /// <paramref name="result"/>. Returns the body with those tags removed, or null if nothing but tag(s)
        /// remained (the common case - a pure <c>.ReportIssue(...)</c> WriteLine has no other text).
        /// </summary>
        public static string? Scan(string? body, ScanResult result)
        {
            if (string.IsNullOrEmpty(body) || body!.IndexOf('<') < 0)
                return body;

            var afterIssues = StripTag(body!, IssueTagPattern, m => ApplyIssue(m.Groups["attrs"].Value, m.Groups["message"].Value, result));

            var text = afterIssues.Trim();
            return text.Length == 0 ? null : text;
        }

        private static string StripTag(string body, Regex pattern, Action<Match> onMatch)
        {
            var remaining = new StringBuilder();
            var pos = 0;

            foreach (Match match in pattern.Matches(body))
            {
                remaining.Append(body, pos, match.Index - pos);
                onMatch(match);
                pos = match.Index + match.Length;
            }

            remaining.Append(body, pos, body.Length - pos);
            return remaining.ToString();
        }

        private static void ApplyIssue(string attrsText, string message, ScanResult result)
        {
            var attrs = new Dictionary<string, string>();
            foreach (Match m in AttributePattern.Matches(attrsText))
                attrs[m.Groups[1].Value] = Unescape(m.Groups[2].Value);

            var category = attrs.TryGetValue("category", out var c) && System.Enum.TryParse<ReportIssueCategory>(c, out var parsed)
                ? parsed
                : ReportIssueCategory.Custom;

            result.Issues.Add(new ReportIssueSummary
            {
                SourcePlugin = attrs.TryGetValue("source", out var src) ? src : "",
                Category = category,
                Severity = attrs.TryGetValue("severity", out var sev) ? sev : null,
                Message = Unescape(message),
                Tags = attrs.TryGetValue("tags", out var tags) && tags.Length > 0 ? [.. tags.Split(',')] : [],
                Hashtags = attrs.TryGetValue("hashtags", out var hashtags) && hashtags.Length > 0 ? [.. hashtags.Split(',')] : []
            });
        }

        private static string Unescape(string value)
            => value.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&amp;", "&");
    }
}
