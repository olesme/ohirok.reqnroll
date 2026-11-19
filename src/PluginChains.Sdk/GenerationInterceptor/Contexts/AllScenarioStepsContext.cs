using System.CodeDom;
using Reqnroll.Generator;
using Reqnroll.Generator.Generation;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly ref struct AllScenarioStepsContext
    {
        public TestClassGenerationContext GenerationContext { get; }
        public CodeMemberMethod Method { get; }
        public ScenarioDefinitionInFeatureFile ScenarioDefinition { get; }
        public List<CodeStatement> Statements { get; }

        internal AllScenarioStepsContext(
            TestClassGenerationContext ctx,
            CodeMemberMethod method,
            ScenarioDefinitionInFeatureFile scenarioDefinition,
            List<CodeStatement> statements)
        {
            GenerationContext = ctx;
            Method = method;
            ScenarioDefinition = scenarioDefinition;
            Statements = statements;
        }

        public void Add(string code)
        {
            if (!string.IsNullOrEmpty(code))
                Statements.Add(new CodeSnippetStatement(code));
        }
    }
}
