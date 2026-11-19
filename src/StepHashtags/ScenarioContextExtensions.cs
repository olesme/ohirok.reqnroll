using Reqnroll;

namespace Ohirok.StepHashtags.ReqnrollPlugin
{
    public static class ScenarioContextExtensions
    {
        /// <summary>
        /// Gets step hashtags for the current step, or empty array if none.
        /// </summary>
        public static string[] GetStepHashtags(this ScenarioContext ctx)
        {
            return ctx.ScenarioContainer.Resolve<StepExecutionContext>().Hashtags;
        }

        /// <summary>
        /// Gets step hashtags matching the predicate for the current step, or empty array if none.
        /// </summary>
        public static string[] GetStepHashtags(this ScenarioContext ctx, Func<string, bool> predicate)
        {
            return [.. ctx.GetStepHashtags().Where(predicate)];
        }

        /// <summary>
        /// Checks if the current step has a specific hashtag.
        /// </summary>
        public static bool HasStepHashtag(this ScenarioContext ctx, string hashtag)
        {
            return ctx.GetStepHashtags().Contains(hashtag, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if the current step has a hashtag matching the predicate.
        /// </summary>
        public static bool HasStepHashtag(this ScenarioContext ctx, Func<string, bool> predicate)
        {
            return ctx.GetStepHashtags().Any(predicate);
        }
    }
}
