using System;
using System.IO;
using Reqnroll;

namespace Ohirok.PluginChains.Common.Reporting
{
    /// <summary>
    /// Extension methods for signaling report issues and embedding rich content into scenario output, recognized by
    /// a future Ohirok reporting formatter. Safe to call with no reporting formatter installed - the calls are then
    /// just inert <see cref="IReqnrollOutputHelper.WriteLine(string)"/>/<see cref="IReqnrollOutputHelper.AddAttachment(string)"/> traffic.
    /// </summary>
    public static class OhirokReportOutputExtensions
    {
        public static void ReportIssue(this IReqnrollOutputHelper output, ReportIssue issue)
            => output.WriteLine(ReportMarkers.WrapIssue(
                issue.Category.ToString(), issue.SourcePlugin, issue.Severity, issue.Tags, issue.Hashtags, issue.Message));

        public static void ReportIssue(
            this IReqnrollOutputHelper output,
            ReportIssueCategory category,
            string sourcePlugin,
            string message,
            string[]? tags = null,
            string[]? hashtags = null,
            string? severity = null)
            => output.ReportIssue(new ReportIssue
            {
                Category = category,
                SourcePlugin = sourcePlugin,
                Message = message,
                Tags = tags,
                Hashtags = hashtags,
                Severity = severity
            });

        /// <summary>
        /// Default behavior is standalone-safe: image bytes are embedded directly into the HTML report payload,
        /// so a copied single .html file remains self-contained. An explicit external storage strategy can still
        /// be configured in reqnroll.json, but the API does not silently leave temp files next to test binaries.
        /// </summary>
        public static void EmbedImage(this IReqnrollOutputHelper output, string filePath, bool expanded = false)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                output.AddAttachment(filePath);
                return;
            }

            EmbedImage(output, File.ReadAllBytes(filePath), Path.GetExtension(filePath), expanded, ResolveMediaType(filePath));
        }

        public static void EmbedImage(this IReqnrollOutputHelper output, byte[] bytes, string fileExtension = ".png", bool expanded = false, string? mediaType = null)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var resolvedMediaType = string.IsNullOrWhiteSpace(mediaType)
                ? ResolveMediaType(fileExtension)
                : mediaType;
            var dataUrl = $"data:{resolvedMediaType};base64,{Convert.ToBase64String(bytes)}";

            output.WriteLine(ReportMarkers.WrapEmbed(
                "image-data",
                expanded ? [("expanded", (string?)"true"), ("data", (string?)dataUrl), ("alt", (string?)"embedded-image")] : [("data", (string?)dataUrl), ("alt", (string?)"embedded-image")]));
        }

        public static void EmbedImageUrl(this IReqnrollOutputHelper output, string url, string? altText = null, bool expanded = false)
        {
            IEnumerable<(string, string?)> attributes = expanded
                ? [("expanded", (string?)"true"), ("url", (string?)url), ("alt", altText)]
                : [("url", (string?)url), ("alt", altText)];
            output.WriteLine(ReportMarkers.WrapEmbed("image-url", attributes));
        }

        public static void EmbedError(this IReqnrollOutputHelper output, string message)
            => EmbedCallout(output, "error", message);

        public static void EmbedWarning(this IReqnrollOutputHelper output, string message)
            => EmbedCallout(output, "warning", message);

        public static void EmbedInfo(this IReqnrollOutputHelper output, string message)
            => EmbedCallout(output, "info", message);

        private static void EmbedCallout(IReqnrollOutputHelper output, string level, string message)
            => output.WriteLine(ReportMarkers.WrapEmbed("callout", [("level", (string?)level)], message));

        /// <summary>
        /// Escape hatch - rendered verbatim, never HTML-encoded, by the reporting formatter.
        /// <paramref name="expanded"/> opens the step's attachments block by default in the report (same
        /// "expanded" marker attribute <see cref="ReportMarkers.WrapEmbed"/> callers can set directly) instead
        /// of the usual collapsed-by-default behavior.
        /// </summary>
        public static void EmbedHtml(this IReqnrollOutputHelper output, string rawHtml, bool expanded = false)
            => output.WriteLine(ReportMarkers.WrapEmbed(
                "raw-html",
                expanded ? [("expanded", (string?)"true")] : [],
                rawHtml));

        private static string ResolveMediaType(string filePathOrExtension)
        {
            var extension = Path.GetExtension(filePathOrExtension).TrimStart('.').ToLowerInvariant();
            return extension switch
            {
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                "gif" => "image/gif",
                "svg" => "image/svg+xml",
                "webp" => "image/webp",
                "bmp" => "image/bmp",
                "txt" => "text/plain",
                "html" or "htm" => "text/html",
                _ => "application/octet-stream"
            };
        }
    }
}
