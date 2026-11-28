using Ohirok.PluginChains.Common;

namespace Ohirok.SoftAssert.Config
{
    public static class SoftAssertConfig
    {
        // PluginConfigProvider caches the section per project; a static copy here would outlive the project.
        private static SoftAssertSection Section => PluginConfigProvider.GetSection<SoftAssertSection>("softassert");

        public static int GenerationInterceptorOrder => Section.GenerationInterceptorOrder;
        public static int BindingInvokerOrder => Section.BindingInvokerOrder;
    }
}
