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
            
        if (!File.Exists(tpkFilePath))
            throw new FileNotFoundException($"Tpk file not found: {tpkFilePath}");
        
        if (!Directory.Exists(outputPath))
            Directory.CreateDirectory(outputPath);
        
        var tpkFile = TpkFile.FromFile(tpkFilePath);
        var blob = tpkFile.GetDataBlob();
        
        Debug.Assert(blob is TpkTypeTreeBlob);

        if (blob is TpkTypeTreeBlob tpkTypeTreeBlob)
        {
            var time = Stopwatch.StartNew();
            TpkUnityTreeNodeFactory.Init(tpkTypeTreeBlob);
            var rootTypeNodesMap = TpkUnityTreeNodeFactory.GetRootTypeNodesAfterVersion(minimalVersionStr);

            TpkUnityTreeNodeFactory.CompactInPlace();
        
            var interfaceGenerator = new InterfaceGenerator();
            
            interfaceGenerator.GenerateInterfaces(outputPath , rootTypeNodesMap);
            Console.WriteLine($"Generated interfaces in {time.Elapsed} s.");
        }
        else
        {
            Console.WriteLine($"Unsupported blob type: {blob.GetType().FullName}, expected TpkTypeTreeBlob.");
        }
    }
}