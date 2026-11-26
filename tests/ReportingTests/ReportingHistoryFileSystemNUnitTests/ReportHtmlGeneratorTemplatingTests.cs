using NUnit.Framework;
using Ohirok.PluginChains.Common;
using Ohirok.Reporting.Config;
using Ohirok.Reporting.Model;
using Ohirok.Reporting.Rendering;

namespace Ohirok.Reporting.History.FileSystem.Tests
{
    /// <summary>
    /// Covers ReportHtmlGenerator's three override points (reporting.template, .css, .js),
    /// which previously had zero test coverage. Each is exercised individually and then composed
    /// together, since the point of making them independent/orthogonal is that they must work in any
    /// combination. The JSON keys were later renamed (templatePath/themeCssPath/jsPath -&gt;
    /// template/css/js) and name-based resolution against a built-in catalog was added - see
    /// CssResolvesBuiltInThemeByName below.
    /// </summary>
    [TestFixture]
    public class ReportHtmlGeneratorTemplatingTests
    {
        private string _workDir = null!;

        [SetUp]
        public void SetUp()
        {
            _workDir = Path.Combine(Path.GetTempPath(), "ohirok-templating-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(_workDir);
            PluginConfigProvider.Reset();
            ReportingConfig.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            PluginConfigProvider.Reset();
            ReportingConfig.Reset();
            try { Directory.Delete(_workDir, recursive: true); } catch { /* best-effort cleanup */ }
        }

        [Test]
        public void NoOverridesConfigured_UsesBuiltInTemplateCssAndJs()
        {
            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain("ohirok-report-tree"), "Expected the built-in template's sidebar mount point.");
            Assert.That(html, Does.Contain("ohirok-report-header"), "Expected the built-in template's header shell.");
        }

        [Test]
        public void CssConfigured_AppendsThemeCssAfterBaseCss()
        {
            const string themeMarker = "--color-passed: #ff00ff; /* theme-marker */";
            LoadConfigWithOverrides(css: WriteFile("theme.css", $":root {{ {themeMarker} }}"));

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain(themeMarker), "Expected the theme CSS to be present in the generated report.");
            Assert.That(html.IndexOf(themeMarker, StringComparison.Ordinal), Is.GreaterThan(html.IndexOf("--color-passed:", StringComparison.Ordinal)),
                "Theme CSS must be appended after the base report.css so its variable redefinitions win the cascade.");
        }

        [Test]
        public void UltravioletTheme_InlinesItsBundledFonts()
        {
            LoadConfigWithOverrides(css: "ultraviolet");

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain("data:font/woff2;base64,"), "The bundled Geist fonts should be inlined as data: URIs.");
            Assert.That(html, Does.Not.Contain("ohirok-asset:"), "No asset reference should be left for the browser to resolve.");
        }

        [Test]
        public void InlineAssetUrls_LeavesAnUnknownAssetAsWritten()
        {
            const string css = "@font-face { src: url(\"ohirok-asset:Fonts/Missing.woff2\"); }";

            Assert.That(ReportHtmlGenerator.InlineAssetUrls(css), Is.EqualTo(css));
        }
        [Test]
        public void CssConfiguredWithBuiltInThemeName_ResolvesByNameInsteadOfPath()
        {
            LoadConfigWithOverrides(css: "ultraviolet");

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            // The theme's own keyframes name: comments (which used to carry the theme's name) are gone once the CSS is minified.
            Assert.That(html.IndexOf("ohirok-neon-sweep", StringComparison.Ordinal), Is.GreaterThanOrEqualTo(0),
                "Expected the built-in ultraviolet theme (resolved by name, not file path) to be embedded.");
        }

        [Test]
        public void JsConfigured_ReplacesBuiltInReportJs()
        {
            const string customJs = "/* custom-js-marker */ console.log('ohirok-custom-js');";
            LoadConfigWithOverrides(js: WriteFile("custom.js", customJs));

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain(customJs), "Expected the custom report.js replacement to be used verbatim.");
            Assert.That(html, Does.Not.Contain("function focusNode"), "Expected the built-in report.js to NOT also be present once js replaces it.");
        }

        [Test]
        public void TemplateConfigured_ReplacesBuiltInShell()
        {
            const string customTemplate = "<html><head><style>{{ohirok:css}}</style><style>{{ohirok:themeCss}}</style></head>" +
                                           "<body data-custom-shell=\"true\"><script>{{ohirok:js}}</script>" +
                                           "<script type=\"application/json\">{{ohirok:json}}</script></body></html>";
            LoadConfigWithOverrides(template: WriteFile("shell.html", customTemplate));

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain("data-custom-shell=\"true\""), "Expected the custom template shell to be used.");
            Assert.That(html, Does.Not.Contain("<div id=\"ohirok-report-sidebar\">"),
                "The built-in shell's own markup must not leak through a full template fork (report.js referencing the same element id by getElementById is expected and fine).");
        }

        [Test]
        public void AllThreeOverridesConfiguredTogether_AllApply()
        {
            const string themeMarker = "/* combined-theme-marker */";
            const string customJs = "/* combined-js-marker */";
            var customTemplate = "<html><head><style>{{ohirok:css}}</style><style>{{ohirok:themeCss}}</style></head>" +
                                  "<body><script>{{ohirok:js}}</script><script type=\"application/json\">{{ohirok:json}}</script></body></html>";

            LoadConfigWithOverrides(
                template: WriteFile("combined-shell.html", customTemplate),
                css: WriteFile("combined-theme.css", themeMarker),
                js: WriteFile("combined-custom.js", customJs));

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain(themeMarker));
            Assert.That(html, Does.Contain(customJs));
        }

        [Test]
        public void TitleConfiguredWithEnvVarPlaceholder_SubstitutesEnvironmentVariable()
        {
            const string varName = "OHIROK_TEST_TITLE_RUN_ID";
            Environment.SetEnvironmentVariable(varName, "run-42");
            try
            {
                LoadConfigWithOverrides(title: $"Build ${{{varName}}}");

                var html = ReportHtmlGenerator.Generate(new ReportProject());

                Assert.That(html, Does.Contain("Build run-42"), "Expected the ${ENV_VAR} placeholder to be substituted with the environment variable's value.");
            }
            finally
            {
                Environment.SetEnvironmentVariable(varName, null);
            }
        }

        [Test]
        public void TitleConfiguredWithUndefinedEnvVarPlaceholder_SubstitutesEmptyString()
        {
            LoadConfigWithOverrides(title: "Build ${OHIROK_TEST_TITLE_DEFINITELY_UNSET}");

            var html = ReportHtmlGenerator.Generate(new ReportProject());

            Assert.That(html, Does.Contain("<title>Build </title>"), "Expected an undefined env var to resolve to an empty string, not be left literal.");
        }

        private string WriteFile(string name, string content)
        {
            var path = Path.Combine(_workDir, name);
            File.WriteAllText(path, content);
            return path;
        }

        private void LoadConfigWithOverrides(string? template = null, string? css = null, string? js = null, string? title = null)
        {
            var configPath = Path.Combine(_workDir, "reqnroll.json");
            File.WriteAllText(configPath,
                $"{{ \"reporting\": {{ " +
                $"\"template\": {ToJsonStringOrNull(template)}, " +
                $"\"css\": {ToJsonStringOrNull(css)}, " +
                $"\"js\": {ToJsonStringOrNull(js)}, " +
                $"\"title\": {ToJsonStringOrNull(title)} " +
                $"}} }}");
            PluginConfigProvider.LoadFromFile(configPath);
        }

        private static string ToJsonStringOrNull(string? value) =>
            value is null ? "null" : $"\"{value.Replace("\\", "\\\\")}\"";
    }
}
