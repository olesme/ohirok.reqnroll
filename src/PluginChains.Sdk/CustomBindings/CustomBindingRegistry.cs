namespace Ohirok.CustomBindings
{
    public class CustomBindingRegistry
    {
        private readonly List<CustomBinding> _customBindings = [];
        private readonly List<CustomHook> _customHooks = [];

        public IEnumerable<CustomBinding> GetCustomBindings()
        {
            return _customBindings;
        }

        public IEnumerable<CustomHook> GetCustomHooks()
        {
            return _customHooks;
        }

        public IEnumerable<CustomHook> GetCustomHooks(string eventKey)
        {
            return _customHooks.Where(h => h.Event == eventKey);
        }

        public void RegisterCustomBinding(CustomBinding binding)
        {
            if (!_customBindings.Contains(binding))
            {
                _customBindings.Add(binding);
            }
        }

        public void RegisterCustomHook(CustomHook hook)
        {
            if (!_customHooks.Contains(hook))
            {
                _customHooks.Add(hook);
            }
        }
    }
}
