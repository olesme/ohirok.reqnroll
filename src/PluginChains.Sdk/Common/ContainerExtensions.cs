using Reqnroll.BoDi;

namespace Ohirok.PluginChains.Common
{
    public static class ContainerExtensions
    {
        public static bool TryResolve<T>(this IObjectContainer container, out T value)
        {
            if (container.IsRegistered<T>())
            {
                value = container.Resolve<T>();
                return true;
            }
            value = default;
            return false;
        }

        public static bool TryResolve<T>(this IObjectContainer container, string name, out T value)
        {
            if (container.IsRegistered<T>(name))
            {
                value = container.Resolve<T>(name);
                return true;
            }
            value = default;
            return false;
        }
    }
}
