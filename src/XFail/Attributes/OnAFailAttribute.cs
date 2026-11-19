namespace Ohirok.XFail.ReqnrollPlugin.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnAFailAttribute : XFailHookAttribute
    {
        public OnAFailAttribute(params string[] filters) : base(filters)
        { Event = XFailConstants.ScenarioAFailedEventKey; }
    }
}
