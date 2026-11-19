using System.Text;
using Ohirok.PluginChains.Common;
using Ohirok.Reporting.Merging;
using Ohirok.Reporting.Rendering;

const string Usage = "Usage: ohirok-report-combine --name <name> --ndjson <path.ndjson> [--ndjson <rerun.ndjson> ...] [--name <name> --ndjson <path.ndjson> ...] --out <combined.html> [--reqnroll-config <reqnroll.json>]\n" +
    "Repeating --ndjson under the same --name chains an ordered base-run + rerun-of-failures sequence (oldest first) into one merged run.";

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine(Usage);
    return 0;
}

var sourceNames = new List<string>();
var sourcePaths = new List<List<string>>();
string? outPath = null;
string? pendingName = null;
string? reqnrollConfigPath = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--name":
            pendingName = args[++i];
            break;
        case "--ndjson":
            var path = args[++i];
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Ndjson file not found: {path}");
                return 1;
            }
            if (pendingName != null)
            {
                sourceNames.Add(pendingName);
                sourcePaths.Add(new List<string> { path });
                pendingName = null;
            }
            else if (sourcePaths.Count > 0)
            {
                sourcePaths[^1].Add(path);
            }
            else
            {
                sourceNames.Add(Path.GetFileNameWithoutExtension(path));
                sourcePaths.Add(new List<string> { path });
            }
            break;
        case "--out":
            outPath = args[++i];
            break;
        case "--reqnroll-config":
            reqnrollConfigPath = args[++i];
            if (!File.Exists(reqnrollConfigPath))
            {
                Console.Error.WriteLine($"Reqnroll config file not found: {reqnrollConfigPath}");
                return 1;
            }
            break;
        default:
            Console.Error.WriteLine($"Unrecognized argument: {args[i]}");
            return 1;
    }
}

if (sourcePaths.Count == 0)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

if (outPath == null)
{
    Console.Error.WriteLine("Missing required --out <combined.html> argument.");
    return 1;
}

if (reqnrollConfigPath != null)
{
    PluginConfigProvider.LoadFromFile(reqnrollConfigPath);
}

var sources = new List<(string Name, IReadOnlyList<string> NdjsonPaths)>();
for (var i = 0; i < sourceNames.Count; i++)
    sources.Add((sourceNames[i], sourcePaths[i]));

var combineResult = ReportRunCombiner.Combine(sources);
var html = ReportHtmlGenerator.Generate(combineResult.Project);
File.WriteAllText(outPath, html, Encoding.UTF8);

foreach (var mergeInfo in combineResult.MergeInfos)
{
    foreach (var orphanedKey in mergeInfo.OrphanedScenarioKeys)
        Console.Error.WriteLine($"Warning: run '{mergeInfo.Name}' - rerun scenario not found in base run: {orphanedKey}");
}

var totalMerged = 0;
foreach (var mergeInfo in combineResult.MergeInfos)
    totalMerged += mergeInfo.MergedScenarioCount;

Console.WriteLine(totalMerged > 0
    ? $"Combined {sources.Count} run(s) into {outPath} ({totalMerged} scenario(s) merged across rerun chains)"
    : $"Combined {sources.Count} run(s) into {outPath}");
return 0;
