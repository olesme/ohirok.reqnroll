using System.CodeDom;
using Gherkin.Ast;
using Reqnroll.Generator;
using Reqnroll.Generator.Generation;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly ref struct ScenarioOwnStepsContext
    {
        public TestClassGenerationContext GenerationContext { get; }
        public CodeMemberMethod Method { get; }
        public ScenarioDefinitionInFeatureFile ScenarioDefinition { get; }
        public List<CodeStatement> Statements { get; }
        public IReadOnlyList<Step> Steps { get; }

        internal ScenarioOwnStepsContext(
            TestClassGenerationContext ctx,
            CodeMemberMethod method,
            ScenarioDefinitionInFeatureFile scenarioDefinition,
            List<CodeStatement> statements,
            IReadOnlyList<Step> steps)
        {
            GenerationContext = ctx;
            Method = method;
            ScenarioDefinition = scenarioDefinition;
            Statements = statements;
            Steps = steps;
        }

        public Scenario Scenario => ScenarioDefinition.Scenario;
        public Rule ParentRule => ScenarioDefinition.Rule;

        public void Add(string code)
        {
            if (!string.IsNullOrEmpty(code))
                Statements.Add(new CodeSnippetStatement(code));
        }
    }
}
