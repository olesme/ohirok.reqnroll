namespace Ohirok.CustomBindings
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class CustomBindingAttribute(params string[] filters) : Attribute
    {
        public int Order { get; set; } = 10000;
        public string[] Filters { get; } = filters ?? [];
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class CustomHookAttribute(params string[] filters) : CustomBindingAttribute(filters)
    {
        public string Event { get; set; }
    }
}
