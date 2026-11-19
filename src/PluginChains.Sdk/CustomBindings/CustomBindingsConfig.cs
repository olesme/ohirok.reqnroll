using Ohirok.PluginChains.Common;

namespace Ohirok.CustomBindings
{
    public static class CustomBindingsConfig
    {
        private static bool? _cached = null;
        public static bool AllowCustomBindings => _cached ??= PluginConfigProvider.GetValue("allowCustomBindings", true);
    }
}
