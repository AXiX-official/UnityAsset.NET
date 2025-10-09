using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.Compiler;
using UnityAsset.NET.TypeTreeHelper.Compiler.Generator;

namespace UnityAsset.NET.TypeTreeHelper;

public static class AssemblyManager
{
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();
    private static readonly List<MetadataReference> References;
    
    private static readonly object CompilationLock = new ();
    private static readonly string AssemblyCachePath = Path.Combine(AppContext.BaseDirectory, "AssemblyCache");
    private static readonly string CachedAssemblyPath = Path.Combine(AssemblyCachePath, "UnityAsset.NET.RuntimeTypes.dll");
    private static readonly string CachedSourcePath = Path.Combine(AssemblyCachePath, "UnityAsset.NET.RuntimeTypes.cs");

    private static CollectibleAssemblyContext? _loadContext;

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
        var referencedAssemblies = Assembly.GetExecutingAssembly().GetReferencedAssemblies();
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(Assembly.GetExecutingAssembly().Location)
        };

        foreach (var assemblyName in referencedAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);
            references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
        References = references;
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
        
        TypeCache.Clear();
        //TypeGenerator.CleanCache();
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

    public static void LoadTypes(List<SerializedType> typesToGenerate)
    {
        CleanCache();
        var compiler = new UnityTypeCompiler(new GenerationOptions{ GenerateOriginalNameAttributes = false});
        var fullSourceCode = compiler.Compile(typesToGenerate);
        Directory.CreateDirectory(AssemblyCachePath);
        File.WriteAllText(CachedSourcePath, fullSourceCode);
        
        var syntaxTree = CSharpSyntaxTree.ParseText(fullSourceCode);
        var compilation = CSharpCompilation.Create(
            assemblyName: "UnityAsset.NET.RuntimeTypes",
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
            foreach (var type in typesToGenerate)
            {
                if (type.Nodes.Count == 0)
                    continue;
                var root = type.Nodes[0];
                var hash = root.GetHashCode(type.Nodes);
                var concreteTypeName = IdentifierSanitizer.SanitizeName($"{root.Type}_{hash}");
                var generatedType = assembly.GetType($"UnityAsset.NET.RuntimeType.{concreteTypeName}");
                if (generatedType != null)
                {
                    TypeCache.TryAdd(type.TypeHash.ToString(), generatedType);
                }
            }
            File.WriteAllBytes(CachedAssemblyPath, assemblyBytes);
        }
    }

    public static Type GetType(SerializedType type)
    {
        if (TypeCache.TryGetValue(type.TypeHash.ToString(), out var cachedType))
        {
            return cachedType;
        }

        throw new Exception($"Unexpected type: {type}");
    }
}