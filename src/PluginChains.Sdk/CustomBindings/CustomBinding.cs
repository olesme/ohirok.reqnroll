using Reqnroll.Bindings.Reflection;

namespace Ohirok.CustomBindings
{
    public class CustomBinding
    {
        public IBindingMethod Method { get; set; }
        public int Order { get; set; }
        public string[] Filters { get; set; }
    }

    public sealed class CustomHook : CustomBinding
    {
        public string Event { get; set; }

        /// <summary>Original attribute for extended filtering</summary>
        public CustomHookAttribute SourceAttribute { get; set; }
    }
}
