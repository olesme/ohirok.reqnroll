using Ohirok.GenerationInterceptor;
using Ohirok.GenerationInterceptor.Contexts;

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    public class StepHashtagInterceptor : GenerationInterceptor.Core.GenerationInterceptor
    {
        private const string ClearMethod = "Ohirok.StepHashtags.Manager.Reset(testRunner);";

        public override int Order => StepHashtagsConfig.GenerationInterceptorOrder;

        public override void BeforeStep(in StepContext ctx)
        {
            if (!ctx.HasHashtags) return;
            ctx.Add($"Ohirok.StepHashtags.Manager.Set(testRunner, {GeneratedCode.StringArray(ctx.Hashtags)});");
        }

        public override void AfterStep(in StepContext ctx)
        {
            if (!ctx.HasHashtags) return;
            ctx.Add(ClearMethod);
        }
    }
}
