using System.Text.Json.Serialization;

namespace Ohirok.Reporting.Config
{
    public class ReportFilterGroup
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("options")]
        public List<ReportFilterOption> Options { get; set; } = new List<ReportFilterOption>();

        [JsonPropertyName("otherLabel")]
        public string OtherLabel { get; set; } = "Other";

        [JsonPropertyName("otherBg")]
        public string OtherBg { get; set; } = "#9e9e9e";

        /// <summary>Text colour of the "other" bucket. Empty (default): readable black or white on <see cref="OtherBg"/>.</summary>
        [JsonPropertyName("otherFg")]
        public string OtherFg { get; set; } = "";
    }
}
