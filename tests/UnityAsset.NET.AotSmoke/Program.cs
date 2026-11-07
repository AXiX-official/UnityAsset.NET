using System.Diagnostics;
using UnityAsset.NET;
using UnityAsset.NET.Enums;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.Types;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper;

var schema = TypeTreeRepr.Create("Base", "TextAsset", [
    TypeTreeRepr.Create("m_Name", "string", [], true),
    TypeTreeRepr.Create("m_Script", "string", [], true),
    TypeTreeRepr.Create("futureField", "int", [], false)], false);
var hash = new Hash128(Enumerable.Repeat((byte)42, 16).ToArray());
var type = new SerializedType((int)AssetClassID.TextAsset, false, -1, null, hash, false, [], null, null, null);
var catalog = new Dictionary<Hash128, UnityTypeSource> { [hash] = new("TextAsset", "2021.3.0f1", schema) };
byte[] Serialize(Action<IWriter> action)
{
    using var stream = new MemoryStream();
    using (var writer = new CustomStreamWriter(stream, leaveOpen: true)) action(writer);
    return stream.ToArray();
}
var bytes = Serialize(w => { w.WriteSizedString("name"); w.Align(4); w.WriteSizedString("payload"); w.Align(4); w.WriteInt32(123); });
var cold = Stopwatch.StartNew();
using var registry = new TypeRegistry();
registry.LoadTypes(catalog);
var factory = registry.GetFactory(type);
var asset = (ITextAsset)factory(new MemoryReader(bytes));
cold.Stop();
if (asset.m_Name != "name" || asset.m_Script != "payload" || asset.m_PathName != null) throw new Exception("Typed read failed.");
asset.m_Name = "edited";
asset.m_Script = "changed payload";
asset.m_PathName = "absent";
var changed = Serialize(asset.Write);
var reread = (ITextAsset)factory(new MemoryReader(changed));
if (reread.m_Name != "edited" || reread.m_Script != "changed payload" || reread.m_PathName != null) throw new Exception("Mutation failed.");
var raw = new NodeData(new MemoryReader(changed), schema).As<Dictionary<string, NodeData>>();
if (raw["futureField"].As<int>() != 123) throw new Exception("Unknown field was lost.");
AdapterChecks.Run();
Console.WriteLine("PASS: typed read, optional field, edits, unknown-field preservation, round trip.");
if (args.Contains("--benchmark"))
{
    const int iterations = 100000;
    for (var i = 0; i < 10000; i++) _ = factory(new MemoryReader(bytes));
    var allocated = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    long checksum = 0;
    for (var i = 0; i < iterations; i++)
    {
        var value = (ITextAsset)factory(new MemoryReader(bytes));
        checksum += value.m_Name.Length + value.m_Script.Length;
    }
    watch.Stop();
    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
#if UNITYASSET_NODEDATA
    const string backend = "NodeData";
#else
    const string backend = "Runtime codegen";
#endif
    Console.WriteLine($"backend={backend}; dynamic={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported}; cold_ms={cold.Elapsed.TotalMilliseconds:F3}; ns_per_object={watch.Elapsed.TotalNanoseconds / iterations:F1}; bytes_per_object={allocated / iterations}; checksum={checksum}");
}

var tpkIndex = Array.IndexOf(args, "--tpk");
if (tpkIndex >= 0)
{
    using var tpk = TpkTypeTreeCatalog.Open(args[tpkIndex + 1]);
    var roots = tpk.GetRootTypeNodes("2017.1.0b1");
    if (!roots.ContainsKey("TextAsset")) throw new Exception("TPK lookup failed.");
    Console.WriteLine($"PASS: TPK catalog ({roots.Count} roots).");
}
var datasetIndex = Array.IndexOf(args, "--dataset");
if (datasetIndex >= 0)
{
    using var manager = new AssetManager(options: new UnitySessionOptions { TpkFilePath = tpkIndex >= 0 ? args[tpkIndex + 1] : null });
    var watch = Stopwatch.StartNew();
    await manager.LoadAsync([args[datasetIndex + 1]]);
    var loadMs = watch.Elapsed.TotalMilliseconds;
    watch.Restart();
    long totalBytes = 0;
    foreach (var entry in manager.LoadedAssets)
    {
        // Factory bypasses Asset.Value's weak-reference cache on every iteration.
        var value = manager.Factory!.Create(entry.Info.Type, entry.DataReader);
        totalBytes += Serialize(value.Write).Length;
    }
    Console.WriteLine($"dataset_assets={manager.LoadedAssets.Count}; load_ms={loadMs:F3}; parse_write_ms={watch.Elapsed.TotalMilliseconds:F3}; serialized_bytes={totalBytes}");
}
