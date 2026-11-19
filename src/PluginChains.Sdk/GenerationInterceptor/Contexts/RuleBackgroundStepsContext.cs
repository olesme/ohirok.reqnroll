using System.CodeDom;
using Gherkin.Ast;
using Reqnroll.Generator;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly ref struct RuleBackgroundStepsContext
    {
        public TestClassGenerationContext GenerationContext { get; }
        public CodeMemberMethod Method { get; }
        public Background Background { get; }
        public Rule Rule { get; }
        public Scenario CurrentScenario { get; }
        public List<CodeStatement> Statements { get; }
        public IReadOnlyList<Step> Steps { get; }

        internal RuleBackgroundStepsContext(
            TestClassGenerationContext ctx,
            CodeMemberMethod method,
            Background background,
            Rule rule,
            Scenario currentScenario,
            List<CodeStatement> statements)
        {
            GenerationContext = ctx;
            Method = method;
            Background = background;
            Rule = rule;
            CurrentScenario = currentScenario;
            Statements = statements;
            Steps = [.. background.Steps];
        }

        public void Add(string code)
        {
            if (!string.IsNullOrEmpty(code))
                Statements.Add(new CodeSnippetStatement(code));
        }
    }
}
