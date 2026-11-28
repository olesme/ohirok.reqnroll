using Ohirok.PluginChains.Common;

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    public static class IgnoreStepConfig
    {
        // PluginConfigProvider caches the section per project; a static copy here would outlive the project.
        private static IgnoreStepSection Config => PluginConfigProvider.GetSection<IgnoreStepSection>("ignoreStep");

        public static bool Enabled => Config.Enabled;
        public static bool MarkSkippedInReport => Config.MarkSkippedInReport;
        public static int BindingInvokerOrder => Config.BindingInvokerOrder;
    }

    internal class IgnoreStepSection
    {
        public bool Enabled { get; set; } = true;
        public bool MarkSkippedInReport { get; set; } = false;
        public int BindingInvokerOrder { get; set; } = 1000;
    }
}
