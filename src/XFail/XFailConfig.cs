using Ohirok.PluginChains.Common;

namespace Ohirok.XFail.ReqnrollPlugin
{
    public enum ExpectedFailureOutput
    {
        None = 0,
        Brief = 1,
        Full = 2
    }

    public static class XFailConfig
    {
        private static XFailSection _cached;
        private static XFailSection Config => _cached ??= PluginConfigProvider.GetSection<XFailSection>("xfail");

        public static bool Enabled => Config.Enabled;
        public static ExpectedFailureOutput OutputMode => Config.OutputMode;
        public static int BindingInvokerOrder => Config.BindingInvokerOrder;
        public static int GenerationInterceptorOrder => Config.GenerationInterceptorOrder;
    }

    internal class XFailSection
    {
        public bool Enabled { get; set; } = true;
        public ExpectedFailureOutput OutputMode { get; set; } = ExpectedFailureOutput.Brief;
        public int BindingInvokerOrder { get; set; } = 2000;
        /// <summary>Order of the scenario-level wrapper among generation interceptors; lower wraps outside (Retry: 50).</summary>
        public int GenerationInterceptorOrder { get; set; } = -1_000_000;
    }
}
