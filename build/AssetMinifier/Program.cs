using NUglify;
using NUglify.JavaScript;

// Usage: AssetMinifier <sourceDir> <outputDir>
// Minifies every .js and .css under sourceDir into the same relative path under outputDir. Local names inside the
// report's single IIFE are shortened too, which is as far as the obfuscation goes: the readable sources stay in the
// repository, the package ships the compact ones.
if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: AssetMinifier <sourceDir> <outputDir>");
    return 2;
}

var sourceDir = Path.GetFullPath(args[0]);
var outputDir = Path.GetFullPath(args[1]);
var jsSettings = new CodeSettings
{
    MinifyCode = true,
    LocalRenaming = LocalRenaming.CrunchAll,
    PreserveImportantComments = true,   // /*! ... */ license notices (Lucide) stay in the shipped file
    TermSemicolons = true,
};
var failed = false;

foreach (var path in Directory.EnumerateFiles(sourceDir, "*.*", SearchOption.AllDirectories))
{
    var extension = Path.GetExtension(path).ToLowerInvariant();
    if (extension is not (".js" or ".css")) continue;

    var source = File.ReadAllText(path);
    var result = extension == ".js" ? Uglify.Js(source, jsSettings) : Uglify.Css(source);
    var relative = Path.GetRelativePath(sourceDir, path);
    if (result.HasErrors)
    {
        failed = true;
        foreach (var error in result.Errors.Where(e => e.IsError))
            Console.Error.WriteLine($"{relative}({error.StartLine},{error.StartColumn}): error MINIFY: {error.Message}");
        continue;
    }

    var target = Path.Combine(outputDir, relative);
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.WriteAllText(target, result.Code);
    Console.WriteLine($"{relative}: {source.Length:N0} -> {result.Code.Length:N0} chars");
}

return failed ? 1 : 0;
