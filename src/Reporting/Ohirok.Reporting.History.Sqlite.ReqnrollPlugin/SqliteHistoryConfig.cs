namespace Ohirok.Reporting.History.Sqlite.ReqnrollPlugin
{
    internal sealed class SqliteHistoryConfig : ReportHistoryConfigBase
    {
        public string DbPath { get; set; } = "reporting-history.db";
    }
}
