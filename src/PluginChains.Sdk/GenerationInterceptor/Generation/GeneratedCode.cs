using Microsoft.CSharp;
using System.CodeDom;
using System.CodeDom.Compiler;

namespace Ohirok.GenerationInterceptor
{
    /// <summary>
    /// Builds C# literals for generated code the way Reqnroll itself does: through CodeDOM,
    /// so quotes, backslashes and control characters are escaped identically.
    /// </summary>
    public static class GeneratedCode
    {
        private static readonly CSharpCodeProvider Provider = new();
        private static readonly CodeGeneratorOptions Options = new();

        public static string StringLiteral(string value)
        {
            using var writer = new StringWriter();
            lock (Provider)   // the generator may process feature files in parallel
                Provider.GenerateCodeFromExpression(new CodePrimitiveExpression(value), writer, Options);
            return writer.ToString();
        }

        public static string StringArray(IEnumerable<string> values) =>
            "new string[] { " + string.Join(", ", values.Select(StringLiteral)) + " }";
    }
}
