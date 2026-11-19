namespace Ohirok.XFail.ReqnrollPlugin.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnXFailAttribute : XFailHookAttribute
    {
        public OnXFailAttribute(params string[] filters) : base(filters)
        { Event = XFailConstants.ScenarioXFailedEventKey; }
    }
}
