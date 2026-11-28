using Ohirok.GenerationInterceptor.Contexts;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Core
{
    public interface IGenerationInterceptor
    {
        int Order { get; }
        int GetEffectiveOrder(IReadOnlyList<string> allTags);

        /// <summary>
        /// Returns the feature document to generate from: the given one, or a changed copy. Runs before anything
        /// is generated, in ascending <see cref="Order"/>, each interceptor receiving the previous one's result.
        /// The test code and the Cucumber messages (Gherkin document, pickles) are both built from the result, so
        /// they always agree. Return the same instance when there is nothing to change.
        /// </summary>
        ReqnrollDocument TransformDocument(ReqnrollDocument document);

        void BeforeTestClass(in TestClassContext ctx);
        void AfterTestClass(in TestClassContext ctx);

        void BeforeScenarioMethod(in ScenarioMethodContext ctx);
        void AfterScenarioMethod(in ScenarioMethodContext ctx);

        // Feature background method
        void BeforeFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx);
        void AfterFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx);

        // Rule background (inline in scenario method)
        void BeforeRuleBackgroundSteps(in RuleBackgroundStepsContext ctx);
        void AfterRuleBackgroundSteps(in RuleBackgroundStepsContext ctx);

        // All steps in scenario method (bg call + rule steps + own steps)
        void BeforeAllScenarioSteps(in AllScenarioStepsContext ctx);
        void AfterAllScenarioSteps(in AllScenarioStepsContext ctx);

        // Only scenario's own steps
        void BeforeScenarioOwnSteps(in ScenarioOwnStepsContext ctx);
        void AfterScenarioOwnSteps(in ScenarioOwnStepsContext ctx);

        void BeforeStep(in StepContext ctx);
        void AfterStep(in StepContext ctx);
    }
}
