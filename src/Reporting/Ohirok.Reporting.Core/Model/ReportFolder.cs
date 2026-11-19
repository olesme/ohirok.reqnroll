namespace Ohirok.Reporting.Model
{
    public class ReportFolder
    {
        public string Name { get; set; } = "";
        public List<ReportFolder> Subfolders { get; set; } = [];
        public List<ReportFeature> Features { get; set; } = [];
    }
}
