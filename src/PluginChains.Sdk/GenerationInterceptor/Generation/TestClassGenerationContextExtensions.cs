using Reqnroll.Generator;

namespace Ohirok.GenerationInterceptor.Generation
{
    // Helper class to have FeatureMessages without an access to non-public original field
    public static class TestClassGenerationContextExtensions
    {
        private const string FEATURE_MESSAGES_KEY = "__MetaDecorator_FeatureMessages";
        private const string FEATURE_MESSAGES_RESOURCE_KEY = "__MetaDecorator_FeatureMessagesResource";

        public static string GetFeatureMessages(this TestClassGenerationContext ctx)
        {
            return ctx.CustomData.TryGetValue(FEATURE_MESSAGES_KEY, out var val)
                ? (string)val : null;
        }

        public static void SetFeatureMessages(this TestClassGenerationContext ctx, string value)
        {
            ctx.CustomData[FEATURE_MESSAGES_KEY] = value;
        }

        public static string GetFeatureMessagesResourceName(this TestClassGenerationContext ctx)
        {
            return ctx.CustomData.TryGetValue(FEATURE_MESSAGES_RESOURCE_KEY, out var val)
                ? (string)val : null;
        }

        public static void SetFeatureMessagesResourceName(this TestClassGenerationContext ctx, string value)
        {
            ctx.CustomData[FEATURE_MESSAGES_RESOURCE_KEY] = value;
        }
    }
}
