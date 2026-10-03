using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.TypeTreeHelper.Compiler;
using UnityAsset.NET.TypeTreeHelper.Model;
using UnityAsset.NET.Types.PreDefined;

namespace UnityAsset.NET.Types;

public sealed class TypeRegistryOptions
{
    public bool WriteGeneratedSource { get; init; } = false;

    public string? CacheDirectory { get; init; }
}

public sealed class TypeRegistry : IDisposable
{
    public const string AssemblyNameSpace = "UnityAsset.NET.RuntimeTypes";

    private sealed class CompilationContext
    {
        public required List<MetadataReference> References { get; init; }
    }

    private static readonly Lazy<CompilationContext> SharedContext =
        new(BuildCompilationContext, LazyThreadSafetyMode.ExecutionAndPublication);

    private static CompilationContext BuildCompilationContext()
    {
        var assemblies = CandidateAssemblies().Distinct().ToList();

        var references = new List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string tpa)
        {
            foreach (var path in tpa.Split(Path.PathSeparator))
            {
                if (!string.IsNullOrEmpty(path))
                    references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        foreach (var assembly in assemblies)
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location))
                references.Add(MetadataReference.CreateFromFile(location));
        }

        return new CompilationContext
        {
            References = references,
        };
    }

    private static IEnumerable<Assembly> CandidateAssemblies()
    {
        var main = Assembly.GetExecutingAssembly();
        yield return main;

        var mainName = main.GetName().Name;
        if (mainName is null)
            yield break;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly == main || assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                continue;

            if (assembly.GetReferencedAssemblies().Any(r => r.Name == mainName))
                yield return assembly;
        }
    }

    #region Session-level state

    private readonly TypeRegistryOptions _options;
    private readonly Lock _compilationLock = new();

    private const string GeneratedSourceFileName = $"{AssemblyNameSpace}.g.cs";

    private ConcurrentDictionary<Hash128, Type> _typeCache = new();
    private CollectibleAssemblyContext? _loadContext;
    private string? _artifactDirectory;
    private bool _disposed;

    private sealed class CollectibleAssemblyContext : AssemblyLoadContext
    {
        public CollectibleAssemblyContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Dependencies all live in the default context; returning null falls back to the default loading mechanism.
            return null;
        }
    }

    public TypeRegistry(TypeRegistryOptions? options = null)
    {
        _options = options ?? new TypeRegistryOptions();
    }

    public string? ArtifactDirectory => _artifactDirectory;

    public string? GeneratedSourcePath => _artifactDirectory is { } directory
        ? Path.Combine(directory, GeneratedSourceFileName)
        : null;

    #endregion

    public void LoadTypes(IReadOnlyDictionary<Hash128, UnityTypeSource> typesToGenerate)
    {
        ArgumentNullException.ThrowIfNull(typesToGenerate);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var context = SharedContext.Value;
        var catalogHash = ComputeCatalogHash(typesToGenerate);

        var compiler = new UnityTypeCompiler();
        var syntax = compiler.Generate(typesToGenerate.Values
            .Select(source => UnitySchemaBuilder.Build(source.ClassName, source.UnityVersion, source.TypeTree)));
        var formattedSource = syntax.NormalizeWhitespace(elasticTrivia: true).ToFullString();

        var syntaxTree = CSharpSyntaxTree.ParseText(formattedSource);

#if DEBUG
        const OptimizationLevel optimizationLevel = OptimizationLevel.Debug;
#else
        const OptimizationLevel optimizationLevel = OptimizationLevel.Release;
#endif

        var compilation = CSharpCompilation.Create(
            assemblyName: AssemblyNameSpace,
            syntaxTrees: [syntaxTree],
            references: context.References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: optimizationLevel));

        lock (_compilationLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var artifactDirectory = CreateArtifactDirectory(catalogHash);
            if (artifactDirectory is not null)
                PersistSource(artifactDirectory, formattedSource);

            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            if (!result.Success)
            {
                throw new InvalidOperationException(BuildCompileErrorMessage(result));
            }

            ms.Seek(0, SeekOrigin.Begin);

            var newContext = new CollectibleAssemblyContext();
            var assembly = newContext.LoadFromStream(ms);

            var typeCache = new ConcurrentDictionary<Hash128, Type>();
            foreach (var (hash128, source) in typesToGenerate)
            {
                var type = source.TypeTree;

                if (type.SubNodes.Length == 0)
                    continue;

                var concreteTypeName = Helper.SanitizeName($"{source.ClassName}_{type.Hash}");
                var generatedType = assembly.GetType($"{AssemblyNameSpace}.{concreteTypeName}");
                if (generatedType != null)
                {
                    typeCache.TryAdd(hash128, generatedType);
                    continue;
                }

                if (source.ClassName == "MonoBehaviour" || Helper.IsPreDefinedType(type))
                    continue;

                throw new InvalidOperationException(
                    $"The generated assembly has no type for {concreteTypeName} (class '{source.ClassName}', catalog " +
                    $"hash {hash128}). The code generator and the registry agree on the generated name only as long " +
                    "as both derive it the same way, so this means the generator skipped a type it was supposed to " +
                    "emit, or that the two name derivations have drifted apart.");
            }

            var unnamedNamedTypes = FindNamedTypesWithoutContract(typesToGenerate, typeCache);
            if (unnamedNamedTypes.Count > 0)
            {
                throw new InvalidOperationException(
                    "These classes carry m_Name but the types generated for them do not implement INamedObject: " +
                    string.Join(", ", unnamedNamedTypes));
            }

            UnloadContext();
            _loadContext = newContext;
            _typeCache = typeCache;
            _artifactDirectory = artifactDirectory;
        }
    }
    
    public Func<IReader, object> GetFactory(SerializedType type)
    {
        ArgumentNullException.ThrowIfNull(type);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (PreDefinedTypeTable.ByUnityTypeName.TryGetValue(type.ToTypeName(), out var preDefinedType))
            return preDefinedType.Create;

        if (_typeCache.TryGetValue(type.TypeHash, out var cachedType))
        {
            return reader => Activator.CreateInstance(cachedType, args: [reader])
                ?? throw new InvalidOperationException(
                    $"Activator.CreateInstance unexpectedly returned null for type: {cachedType.FullName}");
        }

        throw NotInRegistry(type);
    }

    private static NotSupportedException NotInRegistry(SerializedType type)
        => new($"The type {type.Describe()} is not part of this registry: it is neither one of the pre-defined types " +
               "nor a type this session compiled. Load the asset's file through the session that owns this registry " +
               "(AssetManager.Adopt for a file parsed by hand), or check that the type tree is present in the tpk " +
               "database if the file is stripped.");

    public void Dispose()
    {
        lock (_compilationLock)
        {
            if (_disposed)
                return;

            UnloadContext();
            _typeCache = new ConcurrentDictionary<Hash128, Type>();
            _artifactDirectory = null;
            _disposed = true;
        }
    }

    private void UnloadContext()
    {
        _loadContext?.Unload();
        _loadContext = null;
    }

    private static string BuildCompileErrorMessage(Microsoft.CodeAnalysis.Emit.EmitResult result)
    {
        var errorBuilder = new StringBuilder();
        errorBuilder.AppendLine("Failed to compile generated code.");
        errorBuilder.AppendLine("--- COMPILER ERRORS ---");

        var failures = result.Diagnostics.Where(diagnostic =>
            diagnostic.IsWarningAsError ||
            diagnostic.Severity == DiagnosticSeverity.Error);

        foreach (var diagnostic in failures)
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var lineNumber = lineSpan.StartLinePosition.Line + 1;
            var charPosition = lineSpan.StartLinePosition.Character + 1;
            errorBuilder.AppendLine(
                $"Error {diagnostic.Id}: {diagnostic.GetMessage()} at line {lineNumber}, column {charPosition} in {lineSpan.Path}");
        }

        errorBuilder.AppendLine();
        return errorBuilder.ToString();
    }

    private static string ComputeCatalogHash(IReadOnlyDictionary<Hash128, UnityTypeSource> typesToGenerate)
    {
        var builder = new StringBuilder();
        builder.Append('|').Append(AssemblyNameSpace).Append('|');

        foreach (var hex in typesToGenerate.Keys
                     .Select(static key => ToHex(key))
                     .OrderBy(static s => s, StringComparer.Ordinal))
        {
            builder.Append(hex).Append(';');
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash.AsSpan(0, 8)).ToLowerInvariant(); // 16 hex chars
    }
    
    internal static List<string> FindNamedTypesWithoutContract(
        IReadOnlyDictionary<Hash128, UnityTypeSource> types,
        IReadOnlyDictionary<Hash128, Type> typeCache)
    {
        var gaps = new List<string>();
        foreach (var (hash, source) in types)
        {
            if (!source.TypeTree.IsNamed)
                continue;

            var resolved = PreDefinedTypeTable.ByUnityTypeName.TryGetValue(source.ClassName, out var preDefinedType)
                ? preDefinedType.Type
                : typeCache.GetValueOrDefault(hash);
            if (resolved is null || typeof(INamedObject).IsAssignableFrom(resolved))
                continue;

            if (!gaps.Contains(source.ClassName))
                gaps.Add(source.ClassName);
        }

        return gaps;
    }

    private static string ToHex(Hash128 hash)
        => hash.data is { Length: > 0 } data ? Convert.ToHexString(data) : "null";

    private string? CreateArtifactDirectory(string catalogHash)
    {
        if (_options.CacheDirectory is not { } root || !_options.WriteGeneratedSource)
            return null;

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var name = $"{timestamp}_{catalogHash}";

        var directory = Path.Combine(root, name);
        for (var suffix = 2; Directory.Exists(directory); suffix++)
            directory = Path.Combine(root, $"{name}-{suffix}");

        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void PersistSource(string directory, string generatedSource)
        => File.WriteAllText(Path.Combine(directory, GeneratedSourceFileName), generatedSource);
}