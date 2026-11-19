namespace Ohirok.XFail.ReqnrollPlugin
{
    public sealed class XFailScenarioContext
    {
        public XFailMode Mode { get; }
        public Exception? Exception { get; }
        public bool Passed => Exception == null;
        public bool Failed => Exception != null;

        internal XFailScenarioContext(XFailMode mode, Exception? exception)
        {
            Mode = mode;
            Exception = exception;
        }
    }
}
