using System.Diagnostics;
using AssetRipper.Primitives;
using AssetRipper.Tpk;
using AssetRipper.Tpk.TypeTrees;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharpCompress;

namespace UnityAsset.NET.UnityTypeGen;

class Program
{
    private static Dictionary<string, List<TpkUnityTreeNode>> GetRootTypeNodes(TpkTypeTreeBlob blob, string minimalVersionStr)
    {
        UnityVersion.TryParse(minimalVersionStr, out var minimalVersion, out _);
        
        Dictionary<string, HashSet<ushort>> rootTypeNodesMap = new();
        foreach (var info in blob.ClassInformation)
        {
            bool isSupportedVersion = false;
            // versions are sorted 
            for (int i = 0; i < info.Classes.Count; i++)
            {
                var (_, @class) = info.Classes[i];
                if (!isSupportedVersion && i < info.Classes.Count - 1)
                {
                    var (nextVersion, _) = info.Classes[i + 1];
                    if (minimalVersion < nextVersion)
                    {
                        isSupportedVersion = true;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (@class is null)
                    continue;
                
                var name = blob.StringBuffer[@class.Name];

                if ((@class.Flags & TpkUnityClassFlags.HasReleaseRootNode) == 0)
                    continue;
                
                if (!rootTypeNodesMap.ContainsKey(name))
                    rootTypeNodesMap[name] = new HashSet<ushort>();
                
                rootTypeNodesMap[name].Add(@class.ReleaseRootNode);
            }
        }

        return rootTypeNodesMap.ToDictionary(
            kvp => kvp.Key, 
            kvp => kvp.Value.Select(TpkUnityTreeNodeFactory.Create).ToList()
        );
    }
    
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
            var rootTypeNodesMap = GetRootTypeNodes(tpkTypeTreeBlob, minimalVersionStr);

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