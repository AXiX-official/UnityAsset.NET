using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace UnityAsset.NET.Variant.SourceGenerator;

[Generator]
public class VariantGenerator : IIncrementalGenerator 
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all usages of "RefSum<...>" and get the number of type arguments (the arity).
        IncrementalValuesProvider<int> provider = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is GenericNameSyntax { Identifier.ValueText: "RefSum" },
            transform: static (ctx, _) => ((GenericNameSyntax)ctx.Node).TypeArgumentList.Arguments.Count
        );

        // Collect all arities and then process them to get distinct values.
        context.RegisterSourceOutput(provider.Collect(), static (spc, arity) =>
        {
            var distinctArity = new HashSet<int>();
            foreach (var a in arity)
                if (a > 0) 
                    distinctArity.Add(a);

            foreach (var a in distinctArity)
                spc.AddSource($"RefSum{a}.g.cs", GenerateRefSum(a));
        });
    }
    
    private static string GenerateRefSum(int arity)
    {
        // Create Type Parameters (T0, T1, ...)
        var typeParameters = new List<TypeParameterSyntax>();
        var typeArguments = new List<TypeSyntax>();
        for (int i = 0; i < arity; i++)
        {
            var typeParam = $"T{i}";
            typeParameters.Add(SyntaxFactory.TypeParameter(typeParam));
            typeArguments.Add(SyntaxFactory.IdentifierName(typeParam));
        }

        // Create the full generic type name, e.g., RefSum<T0, T1>
        var structType = SyntaxFactory.GenericName(SyntaxFactory.Identifier("RefSum"))
            .AddTypeArgumentListArguments(typeArguments.ToArray());

        // Create Fields:
        // private readonly int _index;
        // private readonly object _value;
        var indexField = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword)))
                .AddVariables(SyntaxFactory.VariableDeclarator("_index")))
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));
        
        var valueField = SyntaxFactory.FieldDeclaration(
            SyntaxFactory.VariableDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))
            .AddVariables(SyntaxFactory.VariableDeclarator("_value")))
        .AddModifiers(SyntaxFactory.Token(SyntaxKind.PrivateKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword));

        // Create Constructor:
        // public RefSum(int index, object value)
        // {
        //     _index = index;
        //     _value = value;
        // }
        var constructor = SyntaxFactory.ConstructorDeclaration("RefSum")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("index")).WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.IntKeyword))),
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("value")).WithType(SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)))
            )
            .WithBody(SyntaxFactory.Block(
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName("_index"),
                        SyntaxFactory.IdentifierName("index"))),
                SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName("_value"),
                        SyntaxFactory.IdentifierName("value")))
            ));
        
        // Create Property: public object Value => _value;
        var valueProperty = SyntaxFactory.PropertyDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.ObjectKeyword)),
                "Value")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithExpressionBody(
                SyntaxFactory.ArrowExpressionClause(
                    SyntaxFactory.IdentifierName("_value")))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
        
        // Create Implicit Operators
        // public static implicit operator RefSumm<T0, T1>(T0 value) => new RefSumm<T0, T1>(0, value);
        var operators = new List<MemberDeclarationSyntax>();
        for (int i = 0; i < arity; i++)
        {
            var typeParam = typeParameters[i];
            var operatorDeclaration = SyntaxFactory.ConversionOperatorDeclaration(
                    SyntaxFactory.Token(SyntaxKind.ImplicitKeyword),
                    structType)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword))
                .AddParameterListParameters(SyntaxFactory.Parameter(SyntaxFactory.Identifier("value")).WithType(SyntaxFactory.IdentifierName(typeParam.Identifier)))
                .WithExpressionBody(
                    SyntaxFactory.ArrowExpressionClause(
                        SyntaxFactory.ObjectCreationExpression(structType)
                            .AddArgumentListArguments(
                                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i))),
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("value"))
                            )
                    )
                )
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
            operators.Add(operatorDeclaration);
        }
        
        // --- Generate Switch Method ---
        var switchParameters = new List<ParameterSyntax>();
        var switchBodyStatements = new List<StatementSyntax>();
        for (int i = 0; i < arity; i++)
        {
            var typeArg = typeArguments[i];
            switchParameters.Add(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier($"f{i}"))
                    .WithType(SyntaxFactory.GenericName("System.Action").AddTypeArgumentListArguments(typeArg))
            );

            switchBodyStatements.Add(
                SyntaxFactory.IfStatement(
                    SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, SyntaxFactory.IdentifierName("_index"),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i))),
                    SyntaxFactory.Block(
                        SyntaxFactory.ExpressionStatement(
                            SyntaxFactory.ConditionalAccessExpression(
                                SyntaxFactory.IdentifierName($"f{i}"),
                                SyntaxFactory.InvocationExpression(SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Invoke")))
                                    .AddArgumentListArguments(SyntaxFactory.Argument(
                                        SyntaxFactory.CastExpression(typeArg, SyntaxFactory.IdentifierName("_value")))))
                        ),
                        SyntaxFactory.ReturnStatement()
                    )
                ));
        }
        switchBodyStatements.Add(SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.IdentifierName("System.InvalidOperationException")).WithArgumentList(SyntaxFactory.ArgumentList())));

        var switchMethod = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Switch")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(switchParameters.ToArray())
            .WithBody(SyntaxFactory.Block(switchBodyStatements));

        // --- Generate Match Method ---
        var matchResultType = SyntaxFactory.TypeParameter("TResult");
        var matchParameters = new List<ParameterSyntax>();
        var matchBodyStatements = new List<StatementSyntax>();
        for (int i = 0; i < arity; i++)
        {
            var typeArg = typeArguments[i];
            matchParameters.Add(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier($"f{i}"))
                    .WithType(SyntaxFactory.GenericName("System.Func").AddTypeArgumentListArguments(typeArg, SyntaxFactory.IdentifierName(matchResultType.Identifier)))
            );

            matchBodyStatements.Add(
                SyntaxFactory.IfStatement(
                    SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, SyntaxFactory.IdentifierName("_index"),
                        SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(i))),
                    SyntaxFactory.ReturnStatement(
                        SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName($"f{i}"))
                            .AddArgumentListArguments(SyntaxFactory.Argument(
                                SyntaxFactory.CastExpression(typeArg, SyntaxFactory.IdentifierName("_value")))))
                ));
        }
        matchBodyStatements.Add(SyntaxFactory.ThrowStatement(SyntaxFactory.ObjectCreationExpression(
            SyntaxFactory.IdentifierName("System.InvalidOperationException")).WithArgumentList(SyntaxFactory.ArgumentList())));

        var matchMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.IdentifierName(matchResultType.Identifier), "Match")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddTypeParameterListParameters(matchResultType)
            .AddParameterListParameters(matchParameters.ToArray())
            .WithBody(SyntaxFactory.Block(matchBodyStatements));

        // Create Struct Declaration
        var structDeclaration = SyntaxFactory.StructDeclaration("RefSum")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword))
            .AddTypeParameterListParameters(typeParameters.ToArray())
            .AddMembers(indexField, valueField, valueProperty, constructor)
            .AddMembers(operators.ToArray())
            .AddMembers(switchMethod, matchMethod);;

        // Create Namespace
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(SyntaxFactory.IdentifierName("UnityAsset.NET"))
            .AddMembers(structDeclaration);

        // Create Compilation Unit
        var compilationUnit = SyntaxFactory.CompilationUnit()
            .AddMembers(namespaceDeclaration)
            .WithLeadingTrivia(SyntaxFactory.Comment("// <auto-generated />"))
            .NormalizeWhitespace();

        return compilationUnit.ToFullString();
    }
}