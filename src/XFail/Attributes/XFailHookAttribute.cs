using Ohirok.CustomBindings;

namespace Ohirok.XFail.ReqnrollPlugin.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class XFailHookAttribute(params string[] filters) : CustomHookAttribute(filters)
    {
    }
}
