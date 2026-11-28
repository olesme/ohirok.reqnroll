using Ohirok.PluginChains.Common;

namespace Ohirok.CustomBindings
{
    public static class CustomBindingsConfig
    {
        public static bool AllowCustomBindings => PluginConfigProvider.GetValue("allowCustomBindings", true);
    }
}
