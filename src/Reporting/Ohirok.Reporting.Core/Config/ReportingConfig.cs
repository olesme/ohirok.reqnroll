using Ohirok.PluginChains.Common;

namespace Ohirok.Reporting.Config
{
    public static class ReportingConfig
    {
        private static ReportingSection _cached;
        private static ReportingSection Section => _cached ??= PluginConfigProvider.GetSection<ReportingSection>("reporting");

        public static List<ReportFilterGroup> Filters => Section.Filters;
        public static string? Title => Section.Title;
        public static string? Template => Section.Template;
        public static string? Css => Section.Css;
        public static string? Js => Section.Js;
        public static string? DefaultTheme => Section.DefaultTheme;
        public static string TimeFormat => string.Equals(Section.TimeFormat?.Trim(), "12h", System.StringComparison.OrdinalIgnoreCase) ? "12h" : "24h";
        // The report UI is English, so its dates default to an English locale rather than the viewer's browser.
        public static string Locale => string.IsNullOrWhiteSpace(Section.Locale) ? "en-GB" : Section.Locale!.Trim();
        // Callouts are one-line messages: folded, their summary line would repeat the whole text anyway.
        public static IReadOnlyList<string> ExpandAttachments => Section.ExpandAttachments ?? ["info", "warning", "error"];

        /// <summary>Clears this class's own cache, independent of PluginConfigProvider.Reset().
        /// Needed because Section caches with "??=" (set once, forever) on top of PluginConfigProvider's
        /// own cache, so a test process that reconfigures reqnroll.json between cases (as
        /// ReportHtmlGeneratorTemplatingTests does) must reset both layers, not just PluginConfigProvider's.
        /// For test use only - production processes read reqnroll.json once and never reconfigure mid-run.</summary>
        public static void Reset() => _cached = null;
    }
}
