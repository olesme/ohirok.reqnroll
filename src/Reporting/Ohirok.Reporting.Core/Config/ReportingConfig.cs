using Ohirok.PluginChains.Common;

namespace Ohirok.Reporting.Config
{
    public static class ReportingConfig
    {
        // PluginConfigProvider caches the section per project; a static copy here would outlive the project.
        private static ReportingSection Section => PluginConfigProvider.GetSection<ReportingSection>("reporting");

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
    }
}
