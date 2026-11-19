namespace Ohirok.Reporting.History.FileSystem.ReqnrollPlugin
{
    internal sealed class FileSystemHistoryConfig : ReportHistoryConfigBase
    {
        public string FilePath { get; set; } = "reporting-history.ndjson";
    }
}
