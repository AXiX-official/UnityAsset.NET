# UnityAsset.NET

---

[![MIT](https://img.shields.io/github/license/AXiX-official/UnityAsset.NET)](https://github.com/AXiX-official/UnityAsset.NET/master/LICENSE)
[![NuGet Stats](https://img.shields.io/nuget/v/UnityAsset.NET.svg)](https://www.nuget.org/packages/UnityAsset.NET)

A .NET library for reading, editing and writing Unity Engine asset files.

Only Unity 2017.x or later is supported.

The library is under active development, so the API can still change between minor versions.

## Example

```csharp
using SixLabors.ImageSharp;
using UnityAsset.NET;
using UnityAsset.NET.AssetHelper;
using UnityAsset.NET.Types.PreDefined.Interfaces;

// A session owns the loaded files, the type catalog and the decompressed block cache.
using var manager = new AssetManager();
await manager.LoadDirectoryAsync(@"D:\Game\StreamingAssets");

foreach (var asset in manager.LoadedAssets)
{
    if (asset.Value is ITexture2D texture)
    {
        var image = manager.DecodeTexture2DToImage(texture);
        image.SaveAsPng(Path.Combine(@"D:\out", $"{texture.m_Name}_{asset.PathId}.png"));
    }
}
```

1. [AssetManager](#assetmanager)
2. [BundleFile and SerializedFile](#bundlefile-and-serializedfile)
3. [UnityCN](#unitycn)
4. [Assets](#assets)
5. [Reading properties through interfaces](#reading-properties-through-interfaces)
6. [Advanced](#advanced)
7. [Testing](#testing)
8. [Credits](#credits)

## AssetManager

`AssetManager` is a session. It owns the file system, the type catalog, the code generated for that catalog and the decompressed block cache; two games loaded at the same time are two managers. Everything that needs a type tree — parsing an object, reading its name, editing it — goes through one.

```csharp
using UnityAsset.NET;

using var manager = new AssetManager(
    onError: (path, exception, message) => Console.Error.WriteLine($"{path}: {message}"),
    options: new UnitySessionOptions
    {
        DefaultUnityVersion = "2022.3.62f1",              // for files that declare none
        TpkFilePath = @"D:\type-trees\uncompressed.tpk",  // for stripped type trees
        UnityCnKey = "00112233445566778899aabbccddeeff",  // 32 hex characters, for encrypted bundles
        //BlockCacheSize = 512L * 1024 * 1024,            // don't mannually set this unless you know what you're doing
    });

await manager.LoadDirectoryAsync(@"D:\Game\StreamingAssets",
    ignoreDuplicatedFiles: true,                          // skip files with the same internal name
    progress: new Progress<LoadProgress>(p => Console.WriteLine($"{p.Percentage,5:F1}% {p.StatusText}"))); // report progress

Console.WriteLine($"{manager.Version} / {manager.BuildTarget}");
Console.WriteLine($"{manager.LoadedFiles.Count} files, " +
                  $"{manager.LoadedAssets.Count} assets, " +
                  $"{manager.LoadedTypes.Count} types");
```

`LoadAsync` takes paths or `IVirtualFileInfo` objects (for a custom file system), and `LoadDirectoryAsync` walks a folder. Files are classified by content, not by extension; anything that is neither a bundle nor a serialized file is skipped, and per-file failures are reported to `onError` instead of ending the load.

Everything a session can be configured with:

| option | falls back to | meaning |
|---|---|---|
| `UnityCnKey` | `Setting.DefaultUnityCNKey` | UnityCN decryption key, 32 hex characters |
| `DefaultUnityVersion` | `Setting.DefaultUnityVerion` | revision assumed when a file declares none |
| `TpkFilePath` | `Setting.DefaultTpkFilePath` (`./uncompressed.tpk`) | type tree database for stripped files |
| `Codegen` | `new TypeRegistryOptions()` | runtime code generation options |
| `BlockCacheSize` | `Setting.DefaultBlockCacheSize` (2 GiB) | decompressed block cache budget, in bytes |

The `Setting` fields stay as the process-wide default: an option left null uses them, so a tool that only ever loads one game does not have to configure anything.

After loading, a session exposes:

* `LoadedFiles` — internal name → `SerializedFile`. A bundle entry is keyed by its internal name (`CAB-...`), a standalone file by its file name.
* `LoadedAssets` — every object in every loaded file, as `Asset`.
* `LoadedTypes` — type hash → class name, Unity version and type tree.
* `Version` / `BuildTarget` — taken from the files that declare them.
* `TryGetLoadedFile(name, out file)` and `LoadStreamingData(streamingInfo)` for cross-file references.
* `VirtualFileToFileMap` — the original `IVirtualFileInfo` → the `BundleFile` or `SerializedFile` opened for it, when you need the container itself.
* `Clear()` to unload everything but keep the manager usable, `Dispose()` to release it.

Two rules to keep in mind:

* **One operation at a time per session.** `LoadAsync`/`LoadDirectoryAsync` take an exclusive flag; a second concurrent load throws. Use one manager per parallel load.
* **Duplicate internal names throw by default.** Pass `ignoreDuplicatedFiles: true` to keep the first file with a name and skip the rest — common when a game folder contains an old and a patched copy of the same bundle.

## BundleFile and SerializedFile

There are two containers:

* **`BundleFile`** — the `UnityFS` archive: header, block table, directory, and compressed data blocks holding serialized files and raw resources.
* **`SerializedFile`** — one `assets` file: a type table, an object table (`PathId`, offset, size, type) and the object payloads.

Both can be opened directly, without a session, for inspection, serialization and CRC work. Parsing the objects inside them, on the other hand, needs a session to supply the type catalog.

### Opening a file directly

```csharp
using UnityAsset.NET;
using UnityAsset.NET.BundleFiles;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.FileSystem.DirectFileSystem;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO.Writer;
using UnityAsset.NET.Types.PreDefined.Interfaces;

// A bundle: header and directory are parsed, the data blocks are still compressed.
var bundle = new BundleFile(new DirectFileInfo(bundlePath));
Console.WriteLine(bundle.Header);
foreach (var entry in bundle.Files)
    Console.WriteLine($"{entry.Info.Path} @ 0x{entry.Info.Offset:X} ({entry.Info.Size} bytes)");

// Turn the serialized entries into SerializedFile objects; other entries stay raw.
bundle.ParseFilesWithTypeConversion();
var fromBundle = bundle.Files.Select(f => f.File).OfType<SerializedFile>().First();

// A serialized file on its own.
var assetsFile = new SerializedFile(new DirectFileInfo(assetsPath));

foreach (var asset in assetsFile.Assets)
    Console.WriteLine($"{asset.TypeName,-16} pathId={asset.PathId,-20} size={asset.Size}");
```

`asset.TypeName`, `PathId` and `Size` come from the object table and cost nothing. Reading `asset.Value` — or `asset.Name` for a named class — parses the payload, which is why that needs a session.

### Loading through a session

The manager does the same work and additionally builds the type catalog, so the loaded files can be parsed, named and edited:

```csharp
await manager.LoadAsync(new List<string> { bundlePath });

foreach (var (name, file) in manager.LoadedFiles)
{
    if (file is SerializedFile sf)
        Console.WriteLine($"{name}: {sf.Assets.Count} objects, {sf.Metadata.Types.Length} types");
}

if (manager.TryGetLoadedFile("CAB-1a2b3c4d", out var found))
    Console.WriteLine(found);
```

### Adding a file parsed by hand

A file opened directly is not part of any session, so `Asset.Value` on it throws. Hand it to a manager with `Adopt`; the name is the key it will be reachable under (and is required for a file that came from a bundle):

```csharp
var assetsFile = new SerializedFile(new DirectFileInfo(assetsPath));

using var manager = new AssetManager();
manager.Adopt(assetsFile, "level0");

// From here it behaves like a file the manager loaded itself.
var mesh = (IMesh)assetsFile.Assets.First(a => a.TypeName == "Mesh").Value;
```

### Writing a bundle

```csharp
// Blocks-info and data blocks can be compressed separately.
bundle.Serialize(outPath);                                            // LZ4HC / LZ4HC
bundle.Serialize(outPath, CompressionType.Lz4, CompressionType.Lz4HC); // or explicitly

// Or into a writer, which is how a bundle is nested in another container.
using var writer = new CustomStreamWriter(File.Create(outPath));
bundle.Serialize(writer);
```

`BundleFile` is an `IFile`, so `bundle.WriteBytes(writer)` does the same when it is embedded in something else.

### Patching the bundle CRC

`CalculateCrc32` computes a CRC32 over the bundle's contents — the concatenation of its entries, read decompressed. `PatchCrc32(target)` appends a small extra entry whose bytes make that CRC come out as `target`, which is how a repacked bundle is made to match a CRC a game expects:

```csharp
var bundle = new BundleFile(new DirectFileInfo(inPath));
Console.WriteLine($"crc32 = 0x{bundle.CalculateCrc32():X8}");

bundle.PatchCrc32(0);
bundle.Serialize(outPath);
```

Both methods read every entry, so every entry has to be an `IReaderProvider`: call them on a freshly opened bundle, or replace the entries you parsed with `MemoryReaderProvider` before patching.

### Writing a serialized file

```csharp
assetsFile.Serialize(outPath);                     // header + metadata + object table + payloads
assetsFile.Serialize(stream, leaveOpen: true);
```

The object table is rebuilt from `Assets` and the payloads are re-laid out; an object whose bytes were not changed is copied through as it was.

### Editing objects and putting the file back

Objects are edited with `FileEdit`, which records operations per `PathId` and produces a complete serialized file when written. To put the result back into its bundle, wrap the edit in `EditedSerializedFile` and replace the entry:

```csharp
using UnityAsset.NET.Edit;

using var manager = new AssetManager();
await manager.LoadAsync(new List<string> { bundlePath });

var bundle = manager.VirtualFileToFileMap.Values.OfType<BundleFile>().Single();
var file = bundle.Files.Select(f => f.File).OfType<SerializedFile>().First();

var edit = new FileEdit(file);
var texture = file.Assets.First(a => a.TypeName == "Texture2D");
edit.Edit<ITexture2D>(texture, t => t.m_Name = "renamed");

// The bundle now pulls the edited file instead of the original entry.
var wrapper = bundle.Files.First(f => ReferenceEquals(f.File, file));
wrapper.File = new EditedSerializedFile(edit);

bundle.Serialize(outPath);
```

The edited serialized file is encoded while the bundle is written, so its new size does not have to be declared anywhere. `FileEdit` is covered in more detail in [Assets](#assets).

## UnityCN

CN version Unity has builtin encryption support called UnityCN. The key is the value the game passes to `AssetBundle.SetAssetBundleDecryptKey`, written as its 16 bytes in hex (32 characters). Set it once per session and everything else — blocks, directory, inner files — is read normally:

```csharp
using var manager = new AssetManager(options: new UnitySessionOptions
{
    UnityCnKey = "00112233445566778899aabbccddeeff",
});

await manager.LoadAsync(new List<string> { bundlePath });
```

Without a session, pass the key to the bundle:

```csharp
var bundle = new BundleFile(new DirectFileInfo(bundlePath), "00112233445566778899aabbccddeeff");
```

`Setting.DefaultUnityCNKey` is the process-wide fallback for both. Opening an encrypted bundle without a key throws with the flag mask that asked for one.

A bundle opened with its key remembers the encryption. Writing it with the key keeps it, writing it without the key drops it:

```csharp
// bundle was opened with its key
bundle.Serialize(outPath, unityCnKey: bundle.UnityCnKey);   // still encrypted
bundle.Serialize(outPath);                                  // written decrypted
// usually game can read the decrypted version as well so you don't have to encrypt it again after editing
```

The old and new flag layouts are recognized from the bundle's Unity revision, including the 16-byte alignment those layouts need.

## Assets

A `SerializedFile` is a table of objects. Each object is an `Asset`: a `PathId`, a serialized type, an offset and size in the file, and — for classes that carry `m_Name` — a name. The AssetBundle object inside a bundle maps some of them to a container path.

| member | what it is |
|---|---|
| `TypeName` | class name from the type table (`Mesh`, `Texture2D`, …); available without parsing |
| `PathId` | the object's id inside the file (`long`) |
| `Info` | `AssetFileInfo`: type, byte offset, byte size |
| `Size` | payload size in bytes |
| `Name` | `m_Name`; empty for classes that are not named |
| `Container` | AssetBundle container path, when the file came from a bundle |
| `SourceFile` | the owning `SerializedFile` |
| `Value` | the parsed object (`IUnityAsset`) |
| `DataReader` | a reader over exactly this object's payload |

Reading is lazy. The object table is parsed when the file is opened; one object's payload is parsed when `Value` (or `Name`) is first asked for, and the result is cached weakly, so it survives as long as something holds it.

```csharp
using UnityAsset.NET.Types.PreDefined;
using UnityAsset.NET.Types.PreDefined.Interfaces;

var asset = manager.LoadedAssets.First(a => a.TypeName == "Material");

Console.WriteLine(asset.TypeName);    // "Material" — no payload read
Console.WriteLine(asset.PathId);
Console.WriteLine(asset.Size);
Console.WriteLine(asset.Container);   // "Assets/.../foo.mat", or ""

// Parses the payload once, then returns the cached object.
IUnityAsset value = asset.Value;
```

If you want a fresh parse instead of the cached one, the session's factory can do it directly (it is available once a load has built the catalog):

```csharp
var fresh = manager.Factory!.Create(asset.Info.Type, asset.DataReader);
```

### Raw payload

```csharp
byte[] payload = asset.DataReader.ReadBytes((int)asset.Size);
```

`DataReader` is a new reader over the object's slice every time, so it always starts at the first payload byte. `asset.Write(writer)` copies the payload without parsing it.

### Editing

`Asset.Value` is shared between callers and should be treated as read-only. An edit never mutates it: `FileEdit` records operations per `PathId` and applies them to a fresh parse of the object while the file is written.

```csharp
using UnityAsset.NET.Edit;
using UnityAsset.NET.IO.Writer;

var file = manager.LoadedFiles.Values.OfType<SerializedFile>().First();
var edit = new FileEdit(file);

// 1. Modify members through the generated interface, on the edit's copy.
edit.Edit<IMaterial>(materialAsset, material =>
{
    material.m_Name = "NewName";
    material.m_CustomRenderQueue = 3000;
});

// 2. Or replace the object wholesale, or with a payload you produced elsewhere.
edit.Set(otherAsset, replacement);   // any IUnityAsset
edit.Set(thirdAsset, payload);       // the complete payload bytes for that type

// 3. Or drop it.
edit.Remove(unusedAsset);

// Inspect before writing.
Console.WriteLine(edit.IsChanged());
edit.Revert(materialAsset);          // drop the operations for one object

using var writer = new CustomStreamWriter(File.Create(outPath), file.Header.Endianness);
edit.WriteBytes(writer);
```

Only objects with operations are re-encoded; every other object is copied byte for byte from the source. The file itself is re-laid out — offsets, sizes and the object table — but the `SerializedFile` you edited is not modified. `WriteBytes` produces new bytes; re-open or re-parse the output to continue from it.

`FileEdit` also has `Fork()` for keeping a copy of the pending edits, and `Discard()` to clear them. `EditedSerializedFile` wraps an edit as an `IFile`, which is how an edited serialized file goes back into a bundle.

## Reading properties through interfaces

`Asset.Value` is typed as `IUnityAsset`, and the generated implementation also implements the interface for its Unity class — cast or pattern-match on it:

```csharp
using UnityAsset.NET.Types.PreDefined.Interfaces;

foreach (var asset in manager.LoadedAssets)
{
    switch (asset.Value)
    {
        case ITexture2D texture:
            Console.WriteLine($"{texture.m_Name}: {texture.m_Width}x{texture.m_Height}");
            break;

        case IMesh mesh:
            Console.WriteLine($"{mesh.m_Name}: {mesh.m_SubMeshes.Length} sub-meshes, " +
                              $"{mesh.m_VertexData.m_VertexCount} vertices");
            break;

        case ITextAsset text:
            Console.WriteLine($"{text.m_Name}: {text.m_Script.Length} characters");
            break;
    }
}
```

The interfaces live in `UnityAsset.NET.Types.PreDefined.Interfaces` and are the union over Unity versions, so a member a particular version does not carry reads as `null`/default and is not written back. That includes version-specific members such as `IMaterial.m_ShaderKeywords`, which is `string?`.

`INamedObject` is the name contract, and `IUnityAsset`/`IUnityObject` are the generic contracts every object satisfies:

```csharp
if (asset.Value is INamedObject named)
    Console.WriteLine(named.m_Name);

// The whole parsed tree as text; useful when exploring a class you do not know.
Console.WriteLine(asset.Value.ToPlainText());
```

References are `PPtr<T>` values; `TryGet` follows them, including into other loaded files:

```csharp
if (materialAsset.Value is IMaterial material &&
    material.m_Shader.TryGet(manager, out var shader))
{
    Console.WriteLine(shader.m_Name);
}
```

`MonoBehaviour` is special: its class-specific fields live in the type tree rather than in a class the library ships, so it is parsed into a node-tree-backed `MonoBehaviour` that implements `IMonoBehaviour` (`m_GameObject`, `m_Enabled`, `m_Script`, `m_Name`). Its `TypeTree` and `NodeData` are public, and `ToPlainText()` dumps the tree as it was parsed, which is the way to see whatever fields that tree actually carries. For a stripped game the tree has to come from the file or from the tpk, and a script class's own tree is not part of the tpk.

The interface tree covers the common Unity classes. A class outside it still parses; it just has the generic contract and `ToPlainText()`, with no class interface to cast to.

## Advanced

### IVirtualFileTransform

`IVirtualFileTransform` is a file-system-level hook that can modify the bytes of a file before the library sees them. It is useful for decryption, decompression or other transformations that are not handled by the library itself.

You can find an example here: [Gf2Transform.cs](https://github.com/AXiX-official/TinyStudio/blob/master/TinyStudio/Games/GF2/Gf2Transform.cs)


### Code generation back ends

Parsing an object needs a concrete C# type per (class, type tree hash). The library builds those types in one of two ways, chosen when the library is compiled:

| | Roslyn (default) | NodeData |
|---|---|---|
| concrete types | generated from the type tree and compiled at load time | one source-generated adapter per interface, compiled with the library |
| parsed object | a generated class with real fields and properties | an adapter over the type tree's node values |
| property read | a field load | a node lookup and unbox, cached after the first read |
| startup | pays the catalog compilation during `LoadAsync` | no compilation |
| needs dynamic code | yes | no |
| generated source dump | yes | no |

Application code does not change between them: both materialize the same interfaces, so `asset.Value is ITexture2D` is the same expression either way. The difference is startup cost against per-property read cost, plus whether the process can execute dynamically compiled code.

The switch is the `UNITYASSET_NODEDATA` compile-time define. It is set automatically when the library is built with `PublishAot=true`, or explicitly with `-p:UnityAssetNodeData=true`.

On the Roslyn back end, `TypeRegistryOptions` can write the generated source out for inspection:

```csharp
using UnityAsset.NET.Types;

var options = new UnitySessionOptions
{
    Codegen = new TypeRegistryOptions
    {
        WriteGeneratedSource = true,
        CacheDirectory = "artifacts/codegen",
    },
};
```

Each load writes its catalog under the directory as `<timestamp>_<catalog-hash>/UnityAsset.NET.RuntimeTypes.g.cs`. The options have no effect on the node back end, which generates its adapters at build time.

### Type tree databases (tpk)

A serialized file can carry its own type trees. A stripped build does not, and then a type tree database is needed to know the shape of a class. The library reads AssetRipper's tpk format; `uncompressed.tpk` from [TypeTreeDumps](https://github.com/AssetRipper/TypeTreeDumps) is the usual choice (you can also download the tpk from [AssetRipper/Tpk](https://github.com/AssetRipper/Tpk)):

```csharp
using var manager = new AssetManager(options: new UnitySessionOptions
{
    TpkFilePath = @"D:\type-trees\uncompressed.tpk",
    DefaultUnityVersion = "2021.3.45f1",   // for files that declare no revision
});
```

The database is only consulted for types the file does not carry itself; a file's own type trees always win. The default path is `./uncompressed.tpk`, relative to the process working directory. One database per path is opened and shared between sessions, and the trees it returns are interned, so two sessions loading the same game do not pay for it twice.

A type found in neither the file nor the database is an error, and the exception lists the class names:

```text
These types are neither carried by their own file nor present in the type tree database (tpk): ...
```

The database can also be used directly, without a manager:

```csharp
using UnityAsset.NET.TypeTreeHelper;

using var tpk = TpkTypeTreeCatalog.Open(@"D:\type-trees\uncompressed.tpk");
var roots = tpk.GetRootTypeNodes("2021.3.45f1");
```

The interface tree checked into `UnityAsset.NET/Types/PreDefined` is generated from such a database by `UnityAsset.NET.UnityTypeGen`; the database passed to a session only affects how a file's missing trees are filled in, not which interfaces exist.

### NativeAOT

NativeAOT cannot run the Roslyn back end, because it compiles C# at run time. Publishing with AOT therefore switches the library to the node back end:

```bash
dotnet publish -c Release -r win-x64 -p:PublishAot=true
```

`PublishAot=true` sets `UnityAssetNodeData=true` for the build (see `Directory.Build.targets`), which defines `UNITYASSET_NODEDATA` and drops the Roslyn package references. To exercise the node back end on a JIT runtime — useful when comparing the two — ask for it explicitly:

```bash
dotnet build -p:UnityAssetNodeData=true
```

The choice is made when the library is compiled, not when a session is created. A prebuilt library that was compiled without the flag always uses Roslyn and cannot be switched at run time, so check which back end a NuGet package was packed with before using it under AOT.

## Testing

The tests were written with an LLM rather than by hand. They are a regression net rather than a specification: they
pin what the code does today, so treat a change in their expectations as something to review rather than to trust.

```bash
dotnet test tests/UnityAsset.NET.Tests/UnityAsset.NET.Tests.csproj
```

## Credits

This project stands on the shoulders of these amazing open-source projects:

- [Studio](https://github.com/RazTools/Studio) by [Razmoth](https://github.com/RazTools)
- [AssetStudio](https://github.com/aelurum/AssetStudio) by [aelurum](https://github.com/aelurum)
- [AssetsTools.NET](https://github.com/nesrak1/AssetsTools.NET) by [nesrak1](https://github.com/nesrak1)
- [UnityPy](https://github.com/K0lb3/UnityPy) by [K0lb3](https://github.com/K0lb3)
- [Tpk](https://github.com/AssetRipper/Tpk) and [TypeTreeDumps](https://github.com/AssetRipper/TypeTreeDumps) by [ds5678](https://github.com/ds5678)
- [RustyAssetBundleEXtractor](https://github.com/UniversalGameExtraction/RustyAssetBundleEXtractor) by [UniversalGameExtraction](https://github.com/UniversalGameExtraction)
