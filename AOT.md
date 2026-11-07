# AOT and NodeData

Build the library from source with `-p:UnityAssetNodeData=true` to select the NodeData backend on a normal JIT runtime. `-p:PublishAot=true` automatically selects it. The corresponding C# symbol is `UNITYASSET_NODEDATA`, which can also be defined directly when compiling the library. These are library build settings: defining a symbol in an application does not change an already compiled NuGet DLL. Publish the application using a project reference to this source tree, or package a library built with the fallback setting.

The ordinary build keeps runtime code generation. The fallback build excludes the runtime compiler/assembly-loader implementation of `TypeRegistry`. Roslyn remains a build dependency for the interface generator; unreachable compiler helpers are removed by NativeAOT trimming. No runtime interface proxies, reflection-based constructors, or blanket linker roots are used by the fallback backend.

`NodeAdapterGenerator` generates concrete interface implementations and typed conversion functions during compilation. It consumes the checked-in interfaces, including handwritten partial declarations and generic interfaces, so there is no second TPK-dependent adapter generation step. Run the existing UnityTypeGen tool when updating the interface definitions. Unknown root types retain schema-based reading/writing; named roots also implement `INamedObject`.

Handwritten predefined types (such as GameObject, SpriteAtlas and the vector types) retain their existing direct parsers. MonoBehaviour retains its existing NodeData implementation. The new fallback replaces the types that previously needed runtime-generated code. Typed arrays and nested objects are cached and flushed into their schema nodes before writing, so in-place mutations are preserved. Fields absent in a Unity version keep the optional interface behavior; fields absent from the interface remain in the node tree and survive serialization. Unity maps retain ordering and duplicate keys. RefSum alternatives are selected from the schema, including empty and jagged arrays.

## Validation

```powershell
# Add -p:UnityAssetTargetFramework=net10.0 if only the .NET 10 SDK is installed.
dotnet run --project tests/UnityAsset.NET.AotSmoke -c Release -- --benchmark
dotnet run --project tests/UnityAsset.NET.AotSmoke -c Release -p:UnityAssetNodeData=true -- --benchmark
dotnet publish tests/UnityAsset.NET.AotSmoke -c Release -r win-x64 -p:PublishAot=true -o artifacts/aot-smoke
./artifacts/aot-smoke/UnityAsset.NET.AotSmoke.exe --benchmark --tpk C:/path/uncompressed.tpk
```

The smoke executable checks typed reads, setters, optional fields, preservation of unknown fields, duplicate map keys, nested mutation, empty arrays and version-dependent RefSum array ranks, signedness changes, pointer variants, sanitized names and per-pair map alignment. It can run as an ordinary executable or a native binary without a test runner. CI runs the existing unit suite in both JIT configurations and publishes/runs a Linux NativeAOT binary. The interface generator is built separately as a development tool.

## Benchmarks

```powershell
./benchmarks/Compare.ps1 -Tpk C:/path/uncompressed.tpk
# Add a serialized file or bundle for the dataset benchmark:
./benchmarks/Compare.ps1 -Tpk C:/path/uncompressed.tpk -Dataset C:/path/assets.bundle
```

The script publishes three independent Release outputs and runs five fresh processes per mode. Results are saved under `artifacts/benchmarks/{jit,nodedata,aot}/results.txt`. Use `-Runtime linux-x64` on a Linux host with the NativeAOT prerequisites installed. Cross-compiling between operating systems is not supported by this script.

The synthetic workload uses the same TextAsset schema and payload in every mode. It reports cold registry setup plus first parse, time per parsed object after warmup, managed allocations per object, and a checksum. The factory is invoked directly, bypassing Asset.Value caching. The optional dataset path reports load time separately from one parse/write pass over every asset. TPK is a schema database, not a real asset dataset. Synthetic results should not be interpreted as representative game-asset throughput; use multiple process samples and a real dataset before drawing performance conclusions.

Local validation used .NET 10 on Windows x64 and the supplied TPK (366 roots for 2017.1.0b1). Regenerating interfaces with that TPK and minimum version produced no differences. The local workspace also contains an untracked AssetModificationTests.cs from an earlier branch that references a removed API; that file is preserved and excluded only from local unit-test invocations, not from CI.

## Local synthetic results

Windows x64, .NET 10, Release; median of five fresh processes, 10,000 warmup parses and 100,000 measured parses per process. These are TextAsset microbenchmarks, not real-dataset results. Timing includes parsing and reading two string properties; the cold column includes registry setup and the first parse.

| Mode | Cold setup + first parse (ms) | Parse + access (ns/object) | Managed bytes/object |
| --- | ---: | ---: | ---: |
| jit | 1046.909 | 585.9 | 472 |
| nodedata | 20.810 | 910.2 | 1272 |
| aot | 0.189 | 444.1 | 1168 |

Final local checks: 565/565 unit tests passed in each JIT configuration. All three Release smoke executables passed, including NativeAOT with dynamic code disabled and TPK loading. The final publish emitted no IL trimming/AOT warnings. .NET 11 and Linux CI jobs are configured but were not executed locally.
