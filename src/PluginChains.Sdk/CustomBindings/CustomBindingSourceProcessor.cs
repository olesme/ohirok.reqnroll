using Reqnroll.Bindings.Discovery;
using Reqnroll.Bindings.Reflection;

namespace Ohirok.CustomBindings
{
    public class CustomBindingSourceProcessor(
        IRuntimeBindingSourceProcessor inner,
        CustomBindingRegistry customRegistry) : IRuntimeBindingSourceProcessor
    {
        // IBindingSourceProcessor delegation

        public bool CanProcessTypeAttribute(string attributeTypeName)
            => inner.CanProcessTypeAttribute(attributeTypeName);

        public bool CanProcessMethodAttribute(string attributeTypeName)
            => inner.CanProcessMethodAttribute(attributeTypeName);

        public bool PreFilterType(IEnumerable<string> attributeTypeNames)
            => inner.PreFilterType(attributeTypeNames);

        public bool ProcessType(BindingSourceType bindingSourceType)
            => inner.ProcessType(bindingSourceType);

        public void ProcessTypeDone()
            => inner.ProcessTypeDone();

        public void ProcessMethod(BindingSourceMethod bindingSourceMethod)
        {
            inner.ProcessMethod(bindingSourceMethod);
            ProcessCustomAttributes(bindingSourceMethod);
        }

        public void BuildingCompleted()
            => inner.BuildingCompleted();

        // IRuntimeBindingSourceProcessor

        public void RegisterTypeLoadError(string errorMessage)
            => inner.RegisterTypeLoadError(errorMessage);

        // Custom logic

        private void ProcessCustomAttributes(BindingSourceMethod bindingSourceMethod)
        {
            if (bindingSourceMethod.BindingMethod is not RuntimeBindingMethod runtimeMethod)
                return;

            var methodInfo = runtimeMethod.MethodInfo;

            foreach (var attr in methodInfo.GetCustomAttributes(typeof(CustomBindingAttribute), true))
            {
                if (attr is CustomHookAttribute hookAttr)
                {
                    customRegistry.RegisterCustomHook(new CustomHook
                    {
                        Method = bindingSourceMethod.BindingMethod,
                        Event = hookAttr.Event,
                        Order = hookAttr.Order,
                        Filters = hookAttr.Filters,
                        SourceAttribute = hookAttr
                    });
                }
                else if (attr is CustomBindingAttribute bindingAttr)
                {
                    customRegistry.RegisterCustomBinding(new CustomBinding
                    {
                        Method = bindingSourceMethod.BindingMethod,
                        Order = bindingAttr.Order,
                        Filters = bindingAttr.Filters
                    });
                }
            }
        }
    }
}
