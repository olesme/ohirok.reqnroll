using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Ohirok.Reporting.Config;
using Ohirok.Reporting.Model;

namespace Ohirok.Reporting.Rendering
{
    /// <summary>
    /// Renders the standalone report HTML by substituting {{ohirok:*}} placeholder tokens into a template -
    /// the built-in one (report-template.html, shipped as full readable source so it can be copied and adapted
    /// directly) unless ReportingSection.Template points at a replacement. Three independent, composable
    /// override points, each resolved by name-or-path:
    /// - Template: full fork of the HTML shell.
    /// - Css: CSS appended after the base report.css, so a theme overrides :root/[data-theme] variables (or
    ///   individual selectors) via the cascade without forking report.css - the recommended, sync-safe
    ///   theming mechanism. May be a literal file path OR the name of a built-in theme (case-insensitive;
    ///   see BuiltInCssThemes below).
    /// - Js: full fork of report.js, for themes/examples that need new DOM or behavior, not just style.
    /// Template/Js are full forks with no automatic sync to the base assets - see Rendering/Assets/
    /// CHANGELOG.md, which forkers must track manually. Template/Js currently have no built-in catalog
    /// entries (no shipped built-in template or script exists yet) - they share the identical name-or-path
    /// resolution mechanism as Css purely for consistency, so adding one later needs no new plumbing.
    /// </summary>
    public static class ReportHtmlGenerator
    {
        private static readonly Regex EnvVarPattern = new Regex(@"\$\{([A-Za-z_][A-Za-z0-9_]*)\}", RegexOptions.Compiled);

        private static readonly Dictionary<string, string> BuiltInCssThemes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["cyberpunk-purple"] = "Themes/cyberpunk-purple.css",
            ["cucumber-green"] = "Themes/cucumber-green.css",
            ["midnight-blue"] = "Themes/midnight-blue.css",
            ["chocolate-brown"] = "Themes/chocolate-brown.css",
        };

        private static readonly Dictionary<string, string> BuiltInTemplates = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> BuiltInScripts = new(StringComparer.OrdinalIgnoreCase);

        public static string Generate(ReportProject project)
        {
            var now = DateTimeOffset.Now;
            var json = ReportJsonPayloadBuilder.Build(project, now);
            var css = ReadAsset("report.css");
            var themeCss = ResolveOverride(ReportingConfig.Css, BuiltInCssThemes) ?? string.Empty;
            var js = ResolveOverride(ReportingConfig.Js, BuiltInScripts) ?? ReadAsset("report.js");
            var title = SubstituteEnvVars(ReportingConfig.Title ?? string.Empty);
            if (string.IsNullOrWhiteSpace(title))
                title = "Ohirok Report";
            // No-script fallback only: report.js re-renders it with the same formatter as every other date.
            var generatedAt = now.ToString("ddd d MMM yyyy 'at' HH:mm:ss 'GMT'zzz", CultureInfo.InvariantCulture);
            var defaultTheme = NormalizeDefaultTheme(ReportingConfig.DefaultTheme);

            // json substituted last: it's the only piece of content not under our control, so nothing after it
            // must ever re-scan the result for other {{ohirok:*}} tokens that might coincidentally appear in it.
            return LoadTemplate()
                .Replace("{{ohirok:title}}", WebUtility.HtmlEncode(title))
                .Replace("{{ohirok:generatedDate}}", WebUtility.HtmlEncode(generatedAt))
                .Replace("{{ohirok:defaultTheme}}", defaultTheme)
                .Replace("{{ohirok:css}}", css)
                .Replace("{{ohirok:themeCss}}", themeCss)
                .Replace("{{ohirok:js}}", js)
                .Replace("{{ohirok:json}}", json);
        }

        /// <summary>Substitutes "${ENV_VAR_NAME}" occurrences in the configured title with the
        /// named environment variable's value (an undefined variable resolves to an empty string, matching
        /// the common envsubst convention). Lets a CI pipeline inject e.g. a build/run number without any
        /// report-specific templating language - mirrors reportingHistory.pipelineRunId's own "pipeline
        /// supplies an already-resolved value" pattern. Public so OhirokReportFormatter can apply
        /// the identical substitution to ReportRun.Name - both are derived from the same configured Title and
        /// must resolve identically, or the page's visible title and its tree-root node label would disagree.</summary>
        public static string SubstituteEnvVars(string value) =>
            EnvVarPattern.Replace(value, m => Environment.GetEnvironmentVariable(m.Groups[1].Value) ?? string.Empty);

        /// <summary>Restricts to exactly "light"/"dark" (case-insensitive); anything else
        /// (including unset) resolves to an empty string, which the bootstrap script in report-template.html
        /// treats as "no configured default." This is a safe JS string literal by construction - no
        /// HTML-encoding is needed since it's substituted into a &lt;script&gt; block, not markup, but the
        /// fixed allow-list means it can never be anything other than one of these three literal values.</summary>
        private static string NormalizeDefaultTheme(string? value)
        {
            var normalized = value?.Trim().ToLowerInvariant();
            return normalized == "light" || normalized == "dark" ? normalized : string.Empty;
        }

        private static string LoadTemplate() =>
            ResolveOverride(ReportingConfig.Template, BuiltInTemplates) ?? ReadAsset("report-template.html");

        /// <summary>Shared name-or-path resolution for Template/Css/Js: a configured value is
        /// first checked against the override's own built-in-name catalog (case-insensitive), then treated
        /// as a literal file path. Returns null (letting the caller fall back to the embedded default) when
        /// the value is unset, matches no built-in name, and isn't an existing file.</summary>
        private static string? ResolveOverride(string? value, IReadOnlyDictionary<string, string> builtIns)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (builtIns.TryGetValue(value, out var assetPath))
                return ReadAsset(assetPath);

            return File.Exists(value) ? File.ReadAllText(value) : null;
        }

        private static string ReadAsset(string relativePath)
        {
            var assembly = typeof(ReportHtmlGenerator).Assembly;
            var resourceName = assembly.GetName().Name + ".Rendering.Assets." + relativePath.Replace('/', '.');

            using var stream = assembly.GetManifestResourceStream(resourceName) 
                ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
