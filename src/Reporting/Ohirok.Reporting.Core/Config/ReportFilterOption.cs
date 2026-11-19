using System.Text.Json.Serialization;

namespace Ohirok.Reporting.Config
{
    public class ReportFilterOption
    {
        [JsonPropertyName("label")]
        public string Label { get; set; } = "";

        [JsonPropertyName("match")]
        public string Match { get; set; } = "";

        [JsonPropertyName("bg")]
        public string Bg { get; set; } = "#9e9e9e";

        /// <summary>Text colour. Empty (default): black or white, whichever is readable on <see cref="Bg"/>.</summary>
        [JsonPropertyName("fg")]
        public string Fg { get; set; } = "";
    }
}
