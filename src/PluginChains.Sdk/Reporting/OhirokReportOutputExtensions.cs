using System;
using System.Collections.Generic;
using System.IO;
using Reqnroll;

namespace Ohirok.PluginChains.Common.Reporting
{
    /// <summary>
    /// Extension methods for signaling report issues and embedding rich content into scenario output, recognized by
    /// the Ohirok reporting formatter. Safe to call with no reporting formatter installed - the calls are then
    /// just inert <see cref="IReqnrollOutputHelper.WriteLine(string)"/>/<see cref="IReqnrollOutputHelper.AddAttachment(string)"/> traffic.
    /// <para>Every embed takes an optional <c>expanded</c>: <c>true</c> shows it open in the report, <c>false</c>
    /// keeps it folded, and <c>null</c> (the default) leaves the choice to <c>reporting.expandAttachments</c>.</para>
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
        /// Image bytes are embedded directly into the HTML report payload, so a copied single .html file remains
        /// self-contained; the API never leaves temp files next to test binaries.
        /// </summary>
        public static void EmbedImage(this IReqnrollOutputHelper output, string filePath, bool? expanded = null)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                output.AddAttachment(filePath);
                return;
            }

            EmbedImage(output, File.ReadAllBytes(filePath), Path.GetExtension(filePath), expanded, ResolveMediaType(filePath));
        }

        public static void EmbedImage(this IReqnrollOutputHelper output, byte[] bytes, string fileExtension = ".png", bool? expanded = null, string? mediaType = null)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));

            var resolvedMediaType = string.IsNullOrWhiteSpace(mediaType)
                ? ResolveMediaType(fileExtension)
                : mediaType;
            var dataUrl = $"data:{resolvedMediaType};base64,{Convert.ToBase64String(bytes)}";

            output.WriteLine(ReportMarkers.WrapEmbed("image-data", WithExpanded(expanded, ("data", dataUrl), ("alt", "embedded-image"))));
        }

        public static void EmbedImageUrl(this IReqnrollOutputHelper output, string url, string? altText = null, bool? expanded = null)
            => output.WriteLine(ReportMarkers.WrapEmbed("image-url", WithExpanded(expanded, ("url", url), ("alt", altText))));

        /// <summary>An error callout. It does not fail the scenario.</summary>
        public static void EmbedError(this IReqnrollOutputHelper output, string message, bool? expanded = null)
            => EmbedCallout(output, "error", message, expanded);

        public static void EmbedWarning(this IReqnrollOutputHelper output, string message, bool? expanded = null)
            => EmbedCallout(output, "warning", message, expanded);

        public static void EmbedInfo(this IReqnrollOutputHelper output, string message, bool? expanded = null)
            => EmbedCallout(output, "info", message, expanded);

        private static void EmbedCallout(IReqnrollOutputHelper output, string level, string message, bool? expanded)
            => output.WriteLine(ReportMarkers.WrapEmbed("callout", WithExpanded(expanded, ("level", level)), message));

        /// <summary>Escape hatch - rendered verbatim, never HTML-encoded, by the reporting formatter.</summary>
        public static void EmbedHtml(this IReqnrollOutputHelper output, string rawHtml, bool? expanded = null)
            => output.WriteLine(ReportMarkers.WrapEmbed("raw-html", WithExpanded(expanded), rawHtml));

        private static IEnumerable<(string, string?)> WithExpanded(bool? expanded, params (string, string?)[] attributes)
        {
            if (expanded.HasValue)
                yield return ("expanded", expanded.Value ? "true" : "false");
            foreach (var attribute in attributes)
                yield return attribute;
        }

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
