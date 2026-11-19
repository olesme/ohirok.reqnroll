using Ohirok.CustomBindings;
using Ohirok.SoftAssert.Core;

namespace Ohirok.SoftAssert.Attributes
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public abstract class SoftAssertHookAttribute(params string[] filters) : CustomHookAttribute(filters)
    {
    }

    /// <summary>Fires once per step whose failure was captured instead of aborting the scenario.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnSoftFailureAttribute : SoftAssertHookAttribute
    {
        public OnSoftFailureAttribute(params string[] filters) : base(filters) { Event = SoftAssertConstants.SoftFailureEventKey; }
    }

    /// <summary>Fires once at end of scenario when the collected soft failures are flushed.</summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public sealed class OnSoftFlushAttribute : SoftAssertHookAttribute
    {
        public OnSoftFlushAttribute(params string[] filters) : base(filters) { Event = SoftAssertConstants.SoftFlushEventKey; }
    }
}
