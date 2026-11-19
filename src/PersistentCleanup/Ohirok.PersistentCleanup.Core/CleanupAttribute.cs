using Ohirok.CustomBindings;

namespace Ohirok.PersistentCleanup
{
    /// <summary>
    /// Marks a method as a cleanup action that can be persisted and executed later.
    /// The method must be declared in a class marked with [Binding] (like Reqnroll hooks),
    /// because it is discovered through the custom-binding registry.
    /// Inherits from CustomBindingAttribute for automatic discovery.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class CleanupAttribute(string key = null) : CustomBindingAttribute()
    {
        /// <summary>
        /// Optional key of this cleanup method, unique within its class and parameter list.
        /// If not specified, the method name is used.
        /// A manual key keeps already persisted records valid when the method is renamed.
        /// </summary>
        public string Key { get; } = key;
    }
}
