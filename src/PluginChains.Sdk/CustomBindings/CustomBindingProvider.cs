using Reqnroll.Bindings.Reflection;

namespace Ohirok.CustomBindings
{
    public interface ICustomBindingProvider
    {
        IEnumerable<CustomBinding> GetAllBindings();
        IEnumerable<CustomBinding> GetBindingsForAttribute<TAttribute>()
            where TAttribute : CustomBindingAttribute;
        IEnumerable<CustomHook> GetAllHooks();
        IEnumerable<CustomHook> GetHooksForEvent(string eventKey);
    }

    public sealed class CustomBindingProvider(CustomBindingRegistry registry) : ICustomBindingProvider
    {
        private readonly CustomBindingRegistry _registry = registry;

        public IEnumerable<CustomBinding> GetAllBindings()
        {
            return _registry.GetCustomBindings();
        }

        public IEnumerable<CustomBinding> GetBindingsForAttribute<TAttribute>()
            where TAttribute : CustomBindingAttribute
        {
            return _registry.GetCustomBindings()
                .Where(b => b.Method is RuntimeBindingMethod rbm &&
                           rbm.MethodInfo.GetCustomAttributes(typeof(TAttribute), true).Any());
        }

        public IEnumerable<CustomHook> GetAllHooks()
        {
            return _registry.GetCustomHooks();
        }

        public IEnumerable<CustomHook> GetHooksForEvent(string eventKey)
        {
            return _registry.GetCustomHooks(eventKey)
                .OrderBy(h => h.Order);
        }
    }
}
