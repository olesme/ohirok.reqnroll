using System.CodeDom;
using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Infrastructure;
using Reqnroll.Generator;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly struct StepContext(
    TestClassGenerationContext genCtx,
    List<CodeStatement> statements,
    Step step,
    int index,
    int total)
    {
        private readonly TestClassGenerationContext genCtx = genCtx;
        private readonly List<CodeStatement> statements = statements;
        private readonly Step step = step;
        private readonly int index = index;
        private readonly int total = total;

        public TestClassGenerationContext GenCtx => genCtx;
        public List<CodeStatement> Statements => statements;
        public Step Step => step;
        public int Index => index;
        public int Total => total;
        public bool IsFirst => index == 0;
        public bool IsLast => index == total - 1;
        public ReqnrollFeature Feature => genCtx.Feature;
        public int Line => step.Location.Line;
        public string Keyword => step.Keyword?.Trim();
        public string Text => step.Text;
        public StepArgument Argument => step.Argument;

        public string[] Hashtags => HashtagCache.GetForStep(genCtx, Line);
        public bool HasHashtags => Hashtags.Length > 0;

        public void Add(CodeStatement s) => Statements.Add(s);
        public void Add(string code) => Statements.Add(new CodeSnippetStatement(code));
    }
}
