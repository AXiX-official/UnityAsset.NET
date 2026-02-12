using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.TypeTreeHelper.Compiler;

namespace UnityAsset.NET.TypeTree;

public static class AssemblyManager
{
    private static ConcurrentDictionary<Hash128, Type> TypeCache = new();
    private static readonly List<MetadataReference> References;
    
    private static readonly object CompilationLock = new ();
    private static readonly string AssemblyCachePath = Path.Combine(AppContext.BaseDirectory, "AssemblyCache");
    private static readonly string AssemblyNameSpace = "UnityAsset.NET.RuntimeTypes";
    private static readonly string CachedAssemblyPath = Path.Combine(AssemblyCachePath, $"{AssemblyNameSpace}.dll");
    private static readonly string CachedSourcePath = Path.Combine(AssemblyCachePath, $"{AssemblyNameSpace}.g.cs");
    
    private static CollectibleAssemblyContext? _loadContext;
    
    private static readonly Dictionary<string, Type> PreDefinedTypeMap;
    private static readonly Dictionary<string, Type> PreDefinedInterfaceMap;

    private class CollectibleAssemblyContext : AssemblyLoadContext
    {
        public CollectibleAssemblyContext() : base(isCollectible: true) { }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // For this simple case, we assume dependencies are in the default context.
            // Returning null falls back to the default context's loading mechanism.
            return null;
        }
    }

    static AssemblyManager()
    {
        var references = new List<MetadataReference>();

        var tpa = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator);

        foreach (var path in tpa)
        {
            references.Add(MetadataReference.CreateFromFile(path));
        }

        references.Add(MetadataReference.CreateFromFile(
            Assembly.GetExecutingAssembly().Location));

        References = references;
        
        var thisAssembly = Assembly.GetExecutingAssembly();
        
        PreDefinedTypeMap = thisAssembly
            .GetTypes()
            .Where(t => t is {IsClass : true, Namespace : "UnityAsset.NET.TypeTree.PreDefined.Types"})
            .Where(t => Helper.PreDefinedTypes.Contains(t.Name))
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
        PreDefinedInterfaceMap = thisAssembly
            .GetTypes()
            .Where(t => t is {IsInterface : true, Namespace : "UnityAsset.NET.TypeTree.PreDefined.Interfaces"})
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
    }

    public static void ExportAssembly(string destinationPath)
    {
        lock (CompilationLock)
        {
            if (!File.Exists(CachedAssemblyPath))
            {
                throw new FileNotFoundException("The cached assembly has not been created yet. Generate at least one type first.", CachedAssemblyPath);
            }
            var directory = Path.GetDirectoryName(destinationPath);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);
            }
            File.Copy(CachedAssemblyPath, destinationPath, true);
        }
    }

    public static void CleanCache()
    {
        lock (CompilationLock)
        {
            _loadContext?.Unload();
            _loadContext = null;
        }

        TypeCache = new();
        if (Directory.Exists(AssemblyCachePath))
        {
            foreach (var file in Directory.GetFiles(AssemblyCachePath))
            {
                File.Delete(file);
            }
            foreach (var dir in Directory.GetDirectories(AssemblyCachePath))
            {
                Directory.Delete(dir, recursive: true);
            }
            Directory.Delete(AssemblyCachePath);
        }
        Directory.CreateDirectory(AssemblyCachePath);
    }

    public static void LoadTypes(List<(Hash128 hash, TypeTreeRepr repr)> typesToGenerate)
    {
        CleanCache();
        var compiler = new UnityTypeCompiler(PreDefinedInterfaceMap);
        var syntax = compiler.Generate(typesToGenerate.Select(i => i.repr));
        Directory.CreateDirectory(AssemblyCachePath);
        var formattedSource = syntax.NormalizeWhitespace(elasticTrivia: true).ToFullString();
        File.WriteAllText(CachedSourcePath, formattedSource, Encoding.UTF8);

        var syntaxTree = CSharpSyntaxTree.ParseText(formattedSource);

        var compilation = CSharpCompilation.Create(
            assemblyName: AssemblyNameSpace,
            syntaxTrees: [syntaxTree],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
#if DEBUG
                optimizationLevel: OptimizationLevel.Debug));
#else
                optimizationLevel: OptimizationLevel.Release));
#endif
        
        lock (CompilationLock)
        {
            // Unload the previous assembly context before loading a new one.
            _loadContext?.Unload();
            _loadContext = new CollectibleAssemblyContext();
            TypeCache.Clear();
            
            using var ms = new MemoryStream();
            var result = compilation.Emit(ms);
            
            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic =>
                    diagnostic.IsWarningAsError ||
                    diagnostic.Severity == DiagnosticSeverity.Error);
            
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine("Failed to compile generated code.");
                errorBuilder.AppendLine("--- COMPILER ERRORS ---");
                foreach (var diagnostic in failures)
                {
                    var lineSpan = diagnostic.Location.GetLineSpan();
                    var lineNumber = lineSpan.StartLinePosition.Line + 1;
                    var charPosition = lineSpan.StartLinePosition.Character + 1;
                    errorBuilder.AppendLine($"Error {diagnostic.Id}: {diagnostic.GetMessage()} at line {lineNumber}, column {charPosition} in {lineSpan.Path}");
                }
                errorBuilder.AppendLine();
            
                throw new InvalidOperationException(errorBuilder.ToString());
            }
            
            ms.Seek(0, SeekOrigin.Begin);
            var assemblyBytes = ms.ToArray();
            ms.Seek(0, SeekOrigin.Begin);
            
            var assembly = _loadContext.LoadFromStream(ms);
            foreach (var (hash128, type) in typesToGenerate)
            {
                if (type.SubNodes.Length == 0)
                    continue;
                var concreteTypeName = Helper.SanitizeName($"{type.TypeName}_{type.Hash}");
                var generatedType = assembly.GetType($"{AssemblyNameSpace}.{concreteTypeName}");
                if (generatedType != null)
                {
                    TypeCache.TryAdd(hash128, generatedType);
                }
            }
            File.WriteAllBytes(CachedAssemblyPath, assemblyBytes);
        }
    }

    public static Type GetType(SerializedType type)
    {
        if (Helper.PreDefinedTypes.Contains(type.ToTypeName()))
        {
            return PreDefinedTypeMap[type.ToTypeName()];
        }
        
        if (TypeCache.TryGetValue(type.TypeHash, out var cachedType))
        {
            return cachedType;
        }
        
        /*if (RoslynBuilderHelper.PreDefinedTypeMap.TryGetValue(type.Nodes[0].Type, out var preDefinedType))
        {
            return preDefinedType;
        }*/

        throw new Exception($"Unexpected type: {type}");
    }
}