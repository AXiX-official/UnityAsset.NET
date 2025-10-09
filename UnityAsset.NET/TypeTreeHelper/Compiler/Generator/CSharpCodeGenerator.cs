using System.Text;
using UnityAsset.NET.TypeTreeHelper.Compiler.Ast;
using UnityAsset.NET.TypeTreeHelper.Compiler.Generator.Strategies;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Generator;

public class CSharpCodeGenerator
{
    private readonly List<IGenerationStrategy> _strategies = new();

    public CSharpCodeGenerator(GenerationOptions options)
    {
        _strategies.Add(new ClassDeclarationStrategy());
        _strategies.Add(new FieldGenerationStrategy());
        _strategies.Add(new ConstructorGenerationStrategy());
        _strategies.Add(new ToPlainTextGenerationStrategy());
        _strategies.Add(new ClassClosingStrategy());
    }

    public string Generate(ClassSyntaxNode classAst, GenerationOptions options)
    {
        var sb = new StringBuilder();
        foreach (var strategy in _strategies)
        {
            strategy.Apply(sb, classAst, options);
        }
        return sb.ToString();
    }
}
