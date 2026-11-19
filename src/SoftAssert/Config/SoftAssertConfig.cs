using Ohirok.PluginChains.Common;

namespace Ohirok.SoftAssert.Config
{
    public static class SoftAssertConfig
    {
        private static SoftAssertSection _cached;
        private static SoftAssertSection Section => _cached ??= PluginConfigProvider.GetSection<SoftAssertSection>("softassert");

        public static int GenerationInterceptorOrder => Section.GenerationInterceptorOrder;
        public static int BindingInvokerOrder => Section.BindingInvokerOrder;
    }
}
