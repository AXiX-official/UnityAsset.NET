using System.Reflection;
using System.Text;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;
using UnityAsset.NET.TypeTreeHelper.Compiler.Builder;
using UnityAsset.NET.TypeTreeHelper.Compiler.Generator;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public class UnityTypeCompiler
{
    private readonly TypeSyntaxBuilder _builder = new();
    private readonly GenerationOptions _options;
    private readonly CSharpCodeGenerator _generator;

    public UnityTypeCompiler(GenerationOptions? options = null)
    {
        _options = options ?? new GenerationOptions();
        _generator = new CSharpCodeGenerator(_options);
    }

    public string Compile(List<SerializedType> serializedTypes)
    {
        var allGeneratedCode = new StringBuilder();
        
        allGeneratedCode.AppendLine("using System;");
        allGeneratedCode.AppendLine("using System.Text;");
        allGeneratedCode.AppendLine("using System.Collections.Generic;");
        allGeneratedCode.AppendLine("using UnityAsset.NET.IO;");
        allGeneratedCode.AppendLine("using UnityAsset.NET.TypeTreeHelper;");
        allGeneratedCode.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined;");
        allGeneratedCode.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;");
        allGeneratedCode.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Types;");
        allGeneratedCode.AppendLine("using UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces;");
        allGeneratedCode.AppendLine();
        allGeneratedCode.AppendLine("namespace UnityAsset.NET.RuntimeType;");
        allGeneratedCode.AppendLine();

        var rootTypeHash = new HashSet<Hash128>();
        
        foreach (var serializedType in serializedTypes)
        {
            if (serializedType.Nodes.Count == 0 || serializedType.Nodes[0].Level != 0) continue;
            
            if (rootTypeHash.Contains(serializedType.TypeHash)) continue;

            var rootNode = serializedType.Nodes[0];
            _builder.Build(rootNode, serializedType.Nodes);
            rootTypeHash.Add(serializedType.TypeHash);
        }

        foreach (var (_, classAst) in _builder.DiscoveredTypes)
        {
            var generatedCode = _generator.Generate(classAst, _options);
            allGeneratedCode.AppendLine(generatedCode);
        }

        return allGeneratedCode.ToString();
    }
}
