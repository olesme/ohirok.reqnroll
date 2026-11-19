using System.CodeDom;
using Gherkin.Ast;
using Reqnroll.Generator;
using Reqnroll.Generator.Generation;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly struct ScenarioMethodContext(
    TestClassGenerationContext genCtx,
    CodeMemberMethod method,
    ScenarioDefinitionInFeatureFile scenarioDef)
    {
        public TestClassGenerationContext GenCtx => genCtx;
        public CodeMemberMethod Method => method;
        public ScenarioDefinitionInFeatureFile ScenarioDef => scenarioDef;
        public CodeStatementCollection Statements => method.Statements;

        public ReqnrollFeature Feature => genCtx.Feature;
        public Scenario Scenario => scenarioDef.Scenario;
        public ScenarioOutline ScenarioOutline => scenarioDef.ScenarioOutline;
        public Rule Rule => scenarioDef.Rule;

        public bool IsOutline => scenarioDef.IsScenarioOutline;
        public string ScenarioName => Scenario?.Name ?? ScenarioOutline?.Name;

        public IEnumerable<Tag> ScenarioTags => Scenario?.Tags ?? ScenarioOutline?.Tags ?? [];
        public IEnumerable<Tag> RuleTags => Rule?.Tags ?? [];
        public IEnumerable<Examples> Examples => ScenarioOutline?.Examples ?? [];

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
