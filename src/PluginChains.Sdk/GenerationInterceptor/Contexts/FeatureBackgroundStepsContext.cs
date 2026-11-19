using System.CodeDom;
using Reqnroll.Generator;
using Gherkin.Ast;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly ref struct FeatureBackgroundStepsContext
    {
        public TestClassGenerationContext GenerationContext { get; }
        public CodeMemberMethod Method { get; }
        public Background Background { get; }
        public List<CodeStatement> Statements { get; }
        public IReadOnlyList<Step> Steps { get; }

        internal FeatureBackgroundStepsContext(
            TestClassGenerationContext ctx,
            CodeMemberMethod method,
            Background background,
            List<CodeStatement> statements)
        {
            GenerationContext = ctx;
            Method = method;
            Background = background;
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
