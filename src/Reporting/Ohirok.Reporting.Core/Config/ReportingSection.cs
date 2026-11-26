using System.Text.Json.Serialization;

namespace Ohirok.Reporting.Config
{
    public class ReportingSection
    {
        [JsonPropertyName("filters")]
        public List<ReportFilterGroup> Filters { get; set; } = [];

        /// <summary>Supports "${ENV_VAR_NAME}" substitution (undefined vars resolve to an
        /// empty string, envsubst-style), resolved in ReportHtmlGenerator.Generate before the existing
        /// HTML-encode/fallback logic. reportingHistory.pipelineRunId resolves references the same way;
        /// beyond this one substitution there is no templating language.</summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>Full replacement for the embedded report-template.html. May
        /// also be the NAME of a built-in template (case-insensitive) instead of a file path; see
        /// ReportHtmlGenerator's shared name-or-path resolver. No built-in templates exist yet - the
        /// resolution mechanism is shared with Css/Js for consistency, not because a catalog exists.</summary>
        [JsonPropertyName("template")]
        public string? Template { get; set; }

        /// <summary>CSS appended after the base report.css inside the template, so a theme can
        /// override :root/[data-theme] variables (and, if needed, individual selectors) via the cascade
        /// without forking report.css itself. The sync-safe default theming mechanism - see
        /// Rendering/Assets/CHANGELOG.md for the base-asset changelog that full forks (Template/Js)
        /// need to track manually. May also be the NAME of one of the built-in themes
        /// ("cyberpunk-purple", "cucumber-green", "midnight-blue", "chocolate-brown", "portal-blue"), case-insensitive, instead of a file
        /// path - see ReportHtmlGenerator's shared name-or-path resolver.</summary>
        [JsonPropertyName("css")]
        public string? Css { get; set; }

        /// <summary>Full replacement for the embedded report.js, mirroring how Template
        /// already replaces report-template.html. A full fork, not a cascade - no automatic sync with the
        /// base report.js; track Rendering/Assets/CHANGELOG.md manually. May also be the NAME
        /// of a built-in script (case-insensitive) instead of a file path; see ReportHtmlGenerator's
        /// shared name-or-path resolver. No built-in scripts exist yet - the resolution mechanism is
        /// shared with Css/Template for consistency, not because a catalog exists.</summary>
        [JsonPropertyName("js")]
        public string? Js { get; set; }

        /// <summary>Overrides the report's initial light/dark pick before any viewer has ever
        /// toggled it (a saved localStorage choice still wins over this). Accepts exactly "light" or
        /// "dark" (case-insensitive); anything else (including unset) falls back to the existing
        /// prefers-color-scheme media-query behavior. Lets a theme (e.g. midnight-blue) declare which
        /// mode it's designed to be seen in first.</summary>
        [JsonPropertyName("defaultTheme")]
        public string? DefaultTheme { get; set; }

        /// <summary>"24h" (default) or "12h". Applies to every date/time the report shows (generation time,
        /// run start/finish, history entries), so they never mix clock styles.</summary>
        [JsonPropertyName("timeFormat")]
        public string? TimeFormat { get; set; }

        /// <summary>BCP 47 locale for month/day names and date order (e.g. "en-US", "uk-UA"). Unset: "en-GB", matching
        /// the English report UI. The clock style always follows <see cref="TimeFormat"/>, whatever the locale.</summary>
        [JsonPropertyName("locale")]
        public string? Locale { get; set; }

        /// <summary>Attachment kinds the report shows open, with no disclosure around them: "info", "warning",
        /// "error" (callouts), "image", "html", "log" (WriteLine text), "text" (text files), "media" (video/audio),
        /// "file", or "all". Unset: the three
        /// callout levels. An explicit expanded="true"/"false" on an embed always wins.</summary>
        [JsonPropertyName("expandAttachments")]
        public List<string>? ExpandAttachments { get; set; }
    }
}
