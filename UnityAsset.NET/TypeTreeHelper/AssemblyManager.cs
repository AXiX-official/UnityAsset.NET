using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityAsset.NET.Files.SerializedFiles;

namespace UnityAsset.NET.TypeTreeHelper;

public static class AssemblyManager
{
    private static readonly TypeGenerator TypeGenerator = new();
    private static readonly ConcurrentDictionary<string, Type> TypeCache = new();
    private static readonly List<MetadataReference> References;
    
    public static bool WriteGeneratedCodeToDisk { get; set; } = false;
    
    private static readonly object CompilationLock = new object();
    private static readonly string AssemblyCachePath = Path.Combine(AppContext.BaseDirectory, "AssemblyCache");
    private static readonly string GeneratedCodePath = Path.Combine(AssemblyCachePath, "GeneratedCode");
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
        TypeGenerator.CleanCache();
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
        var sourceBuilder = new StringBuilder();
        sourceBuilder.AppendLine("using System;");
        sourceBuilder.AppendLine("using System.Text;");
        sourceBuilder.AppendLine("using System.Collections.Generic;");
        sourceBuilder.AppendLine("using UnityAsset.NET.IO;");
        sourceBuilder.AppendLine("using UnityAsset.NET.TypeTreeHelper;");
        sourceBuilder.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined;");
        sourceBuilder.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;");
        sourceBuilder.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;");
        sourceBuilder.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;");
        sourceBuilder.AppendLine();
        sourceBuilder.AppendLine("namespace UnityAsset.NET.RuntimeType;");
        sourceBuilder.AppendLine();
        
        if (WriteGeneratedCodeToDisk)
        {
            Directory.CreateDirectory(GeneratedCodePath);
        }

        foreach (var type in typesToGenerate)
        {
            if (type.Nodes.Count == 0)
                continue;
            var typeSourceCode = TypeGenerator.Generate(type.Nodes);
            if (WriteGeneratedCodeToDisk)
            {
                var root = type.Nodes[0];
                var rootHash64 = root.GetHash64Code(type.Nodes);
                File.WriteAllText(Path.Combine(GeneratedCodePath, $"{TypeGenerator.SanitizeName($"{root.Type}_{rootHash64}")}.cs"), typeSourceCode);
            }
            sourceBuilder.AppendLine(typeSourceCode);
        }
        
        var fullSourceCode = sourceBuilder.ToString();
        Directory.CreateDirectory(AssemblyCachePath);
        File.WriteAllText(CachedSourcePath, fullSourceCode);
        
        var syntaxTree = CSharpSyntaxTree.ParseText(fullSourceCode);
        var compilation = CSharpCompilation.Create(
            assemblyName: "UnityAsset.NET.RuntimeTypes",
            syntaxTrees: new[] { syntaxTree },
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release));

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
                var rootHash64 = root.GetHash64Code(type.Nodes);
                var generatedType = assembly.GetType($"UnityAsset.NET.RuntimeType.{TypeGenerator.SanitizeName($"{root.Type}_{rootHash64}")}");
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