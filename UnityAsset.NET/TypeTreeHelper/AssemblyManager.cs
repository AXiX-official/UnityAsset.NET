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
    
    public static bool WriteGeneratedCodeToDisk { get; set; } = true;
    public static string GeneratedCodePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "GeneratedCode");

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

    public static Type? GetType(SerializedType type)
    {
        if (TypeCache.TryGetValue(type.TypeHash.ToString(), out var cachedType))
        {
            return cachedType;
        }
        
        var sourceCode = TypeGenerator.Generate(type.Nodes);

        if (WriteGeneratedCodeToDisk)
        {
            Directory.CreateDirectory(GeneratedCodePath);
            File.WriteAllText(Path.Combine(GeneratedCodePath, $"{type.Nodes[0].Type}.cs"), sourceCode);
        }
        
        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
        var compilation = CSharpCompilation.Create(
            assemblyName: Path.GetRandomFileName(),
            syntaxTrees: new[] { syntaxTree },
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        
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
                errorBuilder.AppendLine($"Error {diagnostic.Id}: {diagnostic.GetMessage()} at {diagnostic.Location}");
            }
            errorBuilder.AppendLine();
            errorBuilder.AppendLine("--- FAILED SOURCE CODE ---");
            errorBuilder.AppendLine(sourceCode);
            
            throw new InvalidOperationException(errorBuilder.ToString());
        }
        
        ms.Seek(0, SeekOrigin.Begin);
        var assembly = AssemblyLoadContext.Default.LoadFromStream(ms);

        var root = type.Nodes[0];
        var rootHash64 = root.GetHash64Code(type.Nodes);
        var generatedType = assembly.GetType($"UnityAsset.NET.RuntimeType.{TypeGenerator.SanitizeName($"{root.Type}_{rootHash64}")}");
        if (generatedType != null)
        {
            TypeCache.TryAdd(type.TypeHash.ToString(), generatedType);
        }
        
        return generatedType;
    }
}