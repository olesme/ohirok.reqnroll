using Ohirok.GenerationInterceptor.Contexts;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Core
{
    public abstract class GenerationInterceptor : IGenerationInterceptor
    {
        public virtual int Order => 1000;

        public virtual int GetEffectiveOrder(IReadOnlyList<string> allTags) => Order;

        public virtual ReqnrollDocument TransformDocument(ReqnrollDocument document) => document;

        public virtual void BeforeTestClass(in TestClassContext ctx) { }
        public virtual void AfterTestClass(in TestClassContext ctx) { }

        public virtual void BeforeScenarioMethod(in ScenarioMethodContext ctx) { }
        public virtual void AfterScenarioMethod(in ScenarioMethodContext ctx) { }

        public virtual void BeforeFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx) { }
        public virtual void AfterFeatureBackgroundSteps(in FeatureBackgroundStepsContext ctx) { }

        public virtual void BeforeRuleBackgroundSteps(in RuleBackgroundStepsContext ctx) { }
        public virtual void AfterRuleBackgroundSteps(in RuleBackgroundStepsContext ctx) { }

        public virtual void BeforeAllScenarioSteps(in AllScenarioStepsContext ctx) { }
        public virtual void AfterAllScenarioSteps(in AllScenarioStepsContext ctx) { }

        public virtual void BeforeScenarioOwnSteps(in ScenarioOwnStepsContext ctx) { }
        public virtual void AfterScenarioOwnSteps(in ScenarioOwnStepsContext ctx) { }

        public virtual void BeforeStep(in StepContext ctx) { }
        public virtual void AfterStep(in StepContext ctx) { }
    }
}
