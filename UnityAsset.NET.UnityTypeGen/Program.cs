using System.Diagnostics;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.UnityTypeGen;

class Program
{
    static void Main(string[] args)
    {
        var tpkFilePath = args.Length >= 1 ? args[0] : "./uncompressed.tpk";
        var outputPath = args.Length >= 2 ? args[1] : "./Generated";
        var minimalVersionStr = args.Length >= 3 ? args[2] : "2017.1.0b1";
        var schemaTablePath = args.Length >= 4 ? args[3] : DefaultSchemaTablePath(outputPath);

        if (!File.Exists(tpkFilePath))
            throw new FileNotFoundException($"Tpk file not found: {tpkFilePath}");
        
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);

        if (schemaTablePath is null)
            Console.WriteLine("Interface schema table: not written (no TypeTreeHelper project next to the output directory).");
        else
            Console.WriteLine($"Interface schema table: {schemaTablePath}");
        
        var tpkFile = TpkFile.FromFile(tpkFilePath);
        var blob = tpkFile.GetDataBlob();
        
        Debug.Assert(blob is TpkTypeTreeBlob);

        if (blob is TpkTypeTreeBlob tpkTypeTreeBlob)
        {
            var time = Stopwatch.StartNew();
            TpkUnityTreeNodeFactory.Init(tpkTypeTreeBlob);
            var rootTypeNodesMap = TpkUnityTreeNodeFactory.GetRootTypeNodesAfterVersion(minimalVersionStr);

            var interfaceGenerator = new InterfaceGenerator();
            InterfaceGenerator.SchemaTablePath = schemaTablePath;

            interfaceGenerator.GenerateInterfaces(outputPath , rootTypeNodesMap);
            Console.WriteLine($"Generated interfaces in {time.Elapsed} s.");
        }
        else
        {
            Console.WriteLine($"Unsupported blob type: {blob.GetType().FullName}, expected TpkTypeTreeBlob.");
        }
    }

    /// <summary>
    /// Where the schema table belongs when no path is given: the runtime generator reads it from TypeTreeHelper, so
    /// it sits next to that project, which is a sibling of the one the interfaces are generated into.
    /// </summary>
    private static string? DefaultSchemaTablePath(string outputPath)
    {
        var projectDirectory = Path.GetFullPath(Path.Combine(outputPath, "..", "..", "..", ".."));
        var typeTreeHelperDirectory = Path.Combine(projectDirectory, "UnityAsset.NET.TypeTreeHelper");
        return Directory.Exists(typeTreeHelperDirectory)
            ? Path.Combine(typeTreeHelperDirectory, "Schema", "InterfaceSchemaTable.g.cs")
            : null;
    }
}
