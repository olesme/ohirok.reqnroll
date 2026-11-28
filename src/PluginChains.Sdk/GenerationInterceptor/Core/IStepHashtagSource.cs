using Gherkin.Ast;

namespace Ohirok.GenerationInterceptor.Core
{
    /// <summary>
    /// Implemented by a generation interceptor that gives steps hashtags of its own, besides the ones written in comments
    /// above them (for example from configuration). Every interceptor and plugin reading a step's hashtags gets these
    /// first, then the written ones, so for a setting read as its last value the written one wins.
    /// </summary>
    public interface IStepHashtagSource
    {
        /// <summary>The hashtags (without <c>#</c>) this source gives the step, or an empty array.</summary>
        string[] GetStepHashtags(Step step);
    }
}
