using Ohirok.PluginChains.Common;

namespace Ohirok.XFail.ReqnrollPlugin
{
    /// <summary>The keywords of an expected (<c>xfail</c>) and an allowed (<c>afail</c>) failure, shared by the step
    /// hashtags and the scenario tags, and their parsing.</summary>
    internal static class XFailTags
    {
        public static readonly string[] XFail = ["xfail", "expect-fail", "expectfail"];
        public static readonly string[] AFail = ["afail", "allow-fail", "allowfail"];

        /// <summary>Parses <c>xfail</c>, <c>xfail:Type</c>, <c>afail:A|B</c>, ... (with or without a leading <c>@</c> or
        /// <c>#</c>); <paramref name="patterns"/> is empty when any exception is accepted.</summary>
        public static bool TryParse(string tag, out XFailMode mode, out string[] patterns)
        {
            var name = tag.TrimStart('@', '#');
            if (TagValueParser.TryExtractValues(name, XFail, out patterns)) { mode = XFailMode.XFail; return true; }
            if (TagValueParser.TryExtractValues(name, AFail, out patterns)) { mode = XFailMode.AFail; return true; }
            mode = XFailMode.XFail;
            patterns = [];
            return false;
        }

        public static bool Matches(Exception ex, string[] patterns)
        {
            if (patterns == null || patterns.Length == 0) return true;
            return TagValueParser.MatchesPatternRules([ex.GetType().Name, ex.GetType().FullName ?? ""], patterns);
        }
    }
}
