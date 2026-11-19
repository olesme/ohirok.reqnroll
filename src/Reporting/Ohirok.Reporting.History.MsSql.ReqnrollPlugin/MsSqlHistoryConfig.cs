namespace Ohirok.Reporting.History.MsSql.ReqnrollPlugin
{
    internal sealed class MsSqlHistoryConfig : ReportHistoryConfigBase
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
