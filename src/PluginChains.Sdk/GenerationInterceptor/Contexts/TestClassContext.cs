using System.CodeDom;
using Reqnroll.Generator;
using Reqnroll.Parser;

namespace Ohirok.GenerationInterceptor.Contexts
{
    public readonly struct TestClassContext(TestClassGenerationContext genCtx)
    {
        public TestClassGenerationContext GenCtx => genCtx;
        public ReqnrollFeature Feature => genCtx.Feature;
        public ReqnrollDocument Document => genCtx.Document;
        public CodeTypeDeclaration TestClass => genCtx.TestClass;
        public CodeNamespace Namespace => genCtx.Namespace;

        public void AddUsing(string ns)
        {
            foreach (CodeNamespaceImport existing in Namespace.Imports)
                if (existing.Namespace == ns) return;
            Namespace.Imports.Add(new CodeNamespaceImport(ns));
        }

        public void AddClassAttribute(string attributeType, params object[] args)
        {
            var attr = new CodeAttributeDeclaration(attributeType);
            foreach (var arg in args)
                attr.Arguments.Add(new CodeAttributeArgument(new CodePrimitiveExpression(arg)));
            TestClass.CustomAttributes.Add(attr);
        }

        public void AddClassAttribute(CodeAttributeDeclaration attr)
            => TestClass.CustomAttributes.Add(attr);
    }
}
