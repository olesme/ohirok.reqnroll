using System.CodeDom;
using Gherkin.Ast;
using Reqnroll.Generator;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly struct FeatureBackgroundContext(
        TestClassGenerationContext genCtx,
        CodeMemberMethod method,
        Background background,
        List<CodeStatement> statements)
    {
        public TestClassGenerationContext GenCtx => genCtx;
        public CodeMemberMethod Method => method;
        public Background Background => background;
        public List<CodeStatement> Statements => statements;

        public ReqnrollFeature Feature => genCtx.Feature;
        public int StepCount => background?.Steps?.Count() ?? 0;

        public void AddMethodAttribute(string attributeType, params object[] args)
        {
            var attr = new CodeAttributeDeclaration(attributeType);
            foreach (var arg in args)
                attr.Arguments.Add(new CodeAttributeArgument(new CodePrimitiveExpression(arg)));
            Method.CustomAttributes.Add(attr);
        }

        public void AddMethodAttribute(CodeAttributeDeclaration attr)
            => Method.CustomAttributes.Add(attr);

        public void Add(CodeStatement s) => Statements.Add(s);
        public void Add(string code) => Statements.Add(new CodeSnippetStatement(code));
    }
}
