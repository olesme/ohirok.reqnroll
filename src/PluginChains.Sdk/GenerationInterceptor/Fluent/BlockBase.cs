using System.CodeDom;
using Gherkin.Ast;
using Ohirok.GenerationInterceptor.Infrastructure;
using Reqnroll.Generator;

namespace Ohirok.GenerationInterceptor.Fluent
{
    public abstract class BlockBase
    {
        protected readonly TestClassGenerationContext Context;

        public StepBlockInfo Steps { get; }
        public CodeMemberMethod Method { get; }

        internal BlockBase(TestClassGenerationContext ctx, CodeMemberMethod method,
            IReadOnlyList<Step> steps, InjectionScheduler scheduler)
        {
            Context = ctx;
            Method = method;
            Steps = new StepBlockInfo(steps, scheduler,
                idx => HashtagCache.GetForStep(ctx, steps[idx].Location.Line));
        }

        public string[] GetFeatureTags() => Context.Feature?.Tags?
            .Select(t => t.Name.TrimStart('@')).ToArray() ?? [];

        public void AddAttribute(string type, params object[] args)
        {
            var attr = new CodeAttributeDeclaration(type,
                [.. args.Select(a => new CodeAttributeArgument(new CodePrimitiveExpression(a)))]);
            Method.CustomAttributes.Add(attr);
        }

        public void AddUsing(string ns) => Context.Namespace.Imports.Add(new CodeNamespaceImport(ns));

        protected static bool TagsContain(string tag, params string[][] tagSets) =>
            tagSets.SelectMany(t => t).Contains(tag, StringComparer.OrdinalIgnoreCase);
    }
}