namespace Ohirok.Reporting.Model
{
    public class ReportAttachment
    {
        public string MediaType { get; set; } = "";
        public string Body { get; set; } = "";
        public bool IsBase64Encoded { get; set; }
        public string? FileName { get; set; }
        public string? Url { get; set; }
    }
}
