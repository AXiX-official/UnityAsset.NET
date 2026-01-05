using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityAsset.NET.TypeTreeHelper.Compiler.IR;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public class RoslynTypeBuilder
{
    public NamespaceDeclarationSyntax? NamespaceDeclaration;

    public void Build(IEnumerable<ClassTypeInfo> types)
    {
        foreach (var type in types)
        {
            BuildClass(type);
        }
    }
    
    public ClassDeclarationSyntax BuildClass(ClassTypeInfo typeInfo)
    {
        if (NamespaceDeclaration == null)
            throw new NullReferenceException("NamespaceDeclaration is null. Set NamespaceDeclaration before building classes.");

        var classDeclaration = SyntaxFactory.ClassDeclaration(typeInfo.GeneratedClassName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(typeInfo.InterfaceName)));

        var classNameProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "ClassName")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(typeInfo.Name))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));


        classDeclaration = classDeclaration.AddMembers(classNameProperty);
        
        var members = new List<MemberDeclarationSyntax>();

        foreach (var fieldInfo in typeInfo.Fields)
        {
            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(
                    fieldInfo.IsNullable 
                        ? SyntaxFactory.NullableType(fieldInfo.DeclaredTypeSyntax) 
                        : fieldInfo.DeclaredTypeSyntax,
                    fieldInfo.Name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddAccessorListAccessors(
                    SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration).WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                );
            members.Add(propertyDeclaration);
        }
        
        var constructor = BuildConstructor(typeInfo.GeneratedClassName, typeInfo.Fields);
        members.Add(constructor);

        var toAssetNodeMethod = BuildToAssetNodeMethod(typeInfo);
        members.Add(toAssetNodeMethod);

        classDeclaration = classDeclaration.AddMembers(members.ToArray());
        
        NamespaceDeclaration = NamespaceDeclaration.AddMembers(classDeclaration);

        return classDeclaration;
    }

    private MethodDeclarationSyntax BuildToAssetNodeMethod(ClassTypeInfo typeInfo)
    {
        var statements = new List<StatementSyntax>();

        // var rootAssetNode = new AssetNode { Name = name, TypeName = typeInfo.TypeTreeNode.Type };
        statements.Add(
            SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator("rootAssetNode")
                            .WithInitializer(SyntaxFactory.EqualsValueClause(
                                SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("AssetNode"))
                                    .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                                        .AddExpressions(
                                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                                SyntaxFactory.IdentifierName("Name"),
                                                SyntaxFactory.IdentifierName("name")),
                                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                                                SyntaxFactory.IdentifierName("TypeName"),
                                                SyntaxFactory.LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression, 
                                                    SyntaxFactory.Literal(typeInfo.TypeTreeNode.TypeName)
                                                )
                                            )
                                    )
                                )
                            ))
                    )
            )
        );

        foreach (var fieldInfo in typeInfo.Fields)
        {
            statements.AddRange(CreateAssetNodeCreationStatement(fieldInfo.Name, fieldInfo.TypeInfo, "rootAssetNode", fieldInfo.IsNullable, fieldInfo.DeclaredTypeSyntax));
        }

        statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("rootAssetNode")));

        var methodDeclaration = SyntaxFactory.MethodDeclaration(
            SyntaxFactory.IdentifierName("AssetNode?"), "ToAssetNode")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("name")).WithType(SyntaxFactory.ParseTypeName("string")).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Base"))))
            )
            .WithBody(SyntaxFactory.Block(statements));

        return methodDeclaration;
    }

    private IEnumerable<StatementSyntax> CreateAssetNodeCreationStatement(string fieldName, IUnityTypeInfo typeInfo, string parentNode, bool isNullable = false, TypeSyntax? declaredTypeSyntax = null)
    {
        var statements = new List<StatementSyntax>();
        var node = typeInfo.TypeTreeNode;
        var valueAccess = SyntaxFactory.IdentifierName(fieldName);

        switch (typeInfo)
        {
            case PrimitiveTypeInfo p:
                // parentNode.Children.Add(new AssetNode { Name = "...", TypeName = "...", Value = this.FieldName });
                bool isObject = declaredTypeSyntax is IdentifierNameSyntax { Identifier.Text: "Object" };
                
                ExpressionSyntax pValueAccess = isObject
                    ? SyntaxFactory.ParenthesizedExpression(
                        SyntaxFactory.CastExpression(
                            typeInfo switch
                            {
                                PrimitiveTypeInfo pr => pr.ToTypeSyntax(),
                                _ => throw new Exception("Unreachable")
                            },
                            SyntaxFactory.IdentifierName(fieldName)
                        )
                    )
                    : SyntaxFactory.IdentifierName(fieldName);
                
                statements.Add(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(parentNode), SyntaxFactory.IdentifierName("Children")),
                                SyntaxFactory.IdentifierName("Add")))
                        .AddArgumentListArguments(
                            SyntaxFactory.Argument(
                                SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("AssetNode"))
                                    .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                                        .AddExpressions(
                                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("Name"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(node.Name))),
                                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("TypeName"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(node.TypeName))),
                                            SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("Value"), pValueAccess)
                                        )
                                    )
                            )
                        )
                    )
                );
                break;

            case VectorTypeInfo v:
                var vectorNodeName = $"{fieldName}VectorNode";

                bool isOneOf = declaredTypeSyntax is GenericNameSyntax { Identifier.Text: "OneOf" };
                
                ExpressionSyntax vectorTargetExpression = isOneOf
                    ? SyntaxFactory.ParenthesizedExpression(
                        SyntaxFactory.CastExpression(
                            SyntaxFactory.GenericName("List")
                                .AddTypeArgumentListArguments(v.ElementType switch
                                {
                                    PrimitiveTypeInfo p => p.ToTypeSyntax(),
                                    VectorTypeInfo ve => ve.ToTypeSyntax(),
                                    MapTypeInfo m => m.ToTypeSyntax(),
                                    PairTypeInfo pa => pa.ToTypeSyntax(),
                                    ClassTypeInfo c => SyntaxFactory.ParseTypeName(c.InterfaceName),
                                    PredefinedTypeInfo pd => pd.PredefinedTypeSyntax,
                                    GenericPPtrTypeInfo gp => gp.ToTypeSyntax(),
                                    _ => throw new Exception("Unreachable")
                                }),
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(fieldName),
                                SyntaxFactory.IdentifierName("Value")
                            )
                        )
                    )
                    : SyntaxFactory.IdentifierName(fieldName);
                
                statements.Add(
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                            .AddVariables(
                                SyntaxFactory.VariableDeclarator(vectorNodeName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("AssetNode"))
                                            .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                                                .AddExpressions(
                                                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("Name"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(node.Name))),
                                                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("TypeName"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("vector")))
                                                )
                                            )
                                    ))
                            )
                    )
                );

                // foreach (var item in this.FieldName) { ... }
                var itemIdentifier = Helper.SanitizeName($"item{valueAccess}");
                var foreachStatement = SyntaxFactory.ForEachStatement(SyntaxFactory.IdentifierName("var"), itemIdentifier, vectorTargetExpression,
                    SyntaxFactory.Block(CreateAssetNodeCreationStatement(itemIdentifier, v.ElementType, vectorNodeName))
                );
                statements.Add(foreachStatement);
                
                // parentNode.Children.Add(vectorNode);
                statements.Add(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(parentNode), SyntaxFactory.IdentifierName("Children")),
                                SyntaxFactory.IdentifierName("Add")))
                        .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(vectorNodeName)))
                    )
                );
                break;
            
            case MapTypeInfo m:
                var mapNodeName = $"{fieldName}MapNode";
                // var mapNode = new AssetNode { Name = "...", TypeName = "map" };
                statements.Add(
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                            .AddVariables(
                                SyntaxFactory.VariableDeclarator(mapNodeName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("AssetNode"))
                                            .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                                                .AddExpressions(
                                                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("Name"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(node.Name))),
                                                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("TypeName"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("map")))
                                                )
                                            )
                                    ))
                            )
                    )
                );

                // foreach (var pair in this.FieldName) { ... }
                var mapForeach = SyntaxFactory.ForEachStatement(SyntaxFactory.IdentifierName("var"), "pair", valueAccess,
                    SyntaxFactory.Block(CreateAssetNodeCreationStatement("pair", m.PairType, mapNodeName))
                );
                statements.Add(mapForeach);
                
                // parentNode.Children.Add(mapNode);
                statements.Add(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(parentNode), SyntaxFactory.IdentifierName("Children")),
                                SyntaxFactory.IdentifierName("Add")))
                        .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(mapNodeName)))
                    )
                );
                break;

            case PairTypeInfo p:
                 var pairNodeName = $"{fieldName.Replace('.', '_')}PairNode";
                // var pairNode = new AssetNode { Name = "...", TypeName = "pair" };
                statements.Add(
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                            .AddVariables(
                                SyntaxFactory.VariableDeclarator(pairNodeName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        SyntaxFactory.ObjectCreationExpression(SyntaxFactory.IdentifierName("AssetNode"))
                                            .WithInitializer(SyntaxFactory.InitializerExpression(SyntaxKind.ObjectInitializerExpression)
                                                .AddExpressions(
                                                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("Name"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(node.Name))),
                                                    SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, SyntaxFactory.IdentifierName("TypeName"), SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("pair")))
                                                )
                                            )
                                    ))
                            )
                    )
                );
                
                // Handle first and second
                statements.AddRange(CreateAssetNodeCreationStatement($"{fieldName}.Item1", p.Item1Type, pairNodeName));
                statements.AddRange(CreateAssetNodeCreationStatement($"{fieldName}.Item2", p.Item2Type, pairNodeName));

                // parentNode.Children.Add(pairNode);
                statements.Add(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(parentNode), SyntaxFactory.IdentifierName("Children")),
                                SyntaxFactory.IdentifierName("Add")))
                        .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(pairNodeName)))
                    )
                );
                break;

            case ClassTypeInfo:
            case PredefinedTypeInfo:
            case GenericPPtrTypeInfo:
                var childNodeName = $"childNode_{fieldName.Replace('.', '_')}";
                // var childNode = this.FieldName?.ToAssetNode("FieldName");

                bool needValueAccess = declaredTypeSyntax is GenericNameSyntax { Identifier.Text: "OneOf" };
                bool needCast =
                    needValueAccess
                    || declaredTypeSyntax is IdentifierNameSyntax { Identifier.Text: "Object" };
                
                ExpressionSyntax targetExpression = needCast
                    ? SyntaxFactory.ParenthesizedExpression(
                        SyntaxFactory.CastExpression(
                            typeInfo switch
                            {
                                ClassTypeInfo c => SyntaxFactory.ParseTypeName(c.InterfaceName),
                                PredefinedTypeInfo pd => pd.PredefinedTypeSyntax,
                                GenericPPtrTypeInfo gp => gp.ToTypeSyntax(),
                                _ => throw new Exception("Unreachable")
                            },
                            needValueAccess
                            ? SyntaxFactory.MemberAccessExpression(
                                    SyntaxKind.SimpleMemberAccessExpression,
                                    SyntaxFactory.IdentifierName(fieldName),
                                    SyntaxFactory.IdentifierName("Value")
                                )
                            : SyntaxFactory.IdentifierName(fieldName)
                        )
                    )
                    : SyntaxFactory.IdentifierName(fieldName);
                
                ExpressionSyntax invocation = isNullable
                    ? SyntaxFactory.ConditionalAccessExpression(
                        targetExpression,
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberBindingExpression(
                                SyntaxFactory.IdentifierName("ToAssetNode")
                            )
                        ).AddArgumentListArguments(
                            SyntaxFactory.Argument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal(node.Name)
                                )
                            )
                        )
                    )
                    : SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            targetExpression,
                            SyntaxFactory.IdentifierName("ToAssetNode")
                        )
                    ).AddArgumentListArguments(
                        SyntaxFactory.Argument(
                            SyntaxFactory.LiteralExpression(
                                SyntaxKind.StringLiteralExpression,
                                SyntaxFactory.Literal(node.Name)
                            )
                        )
                    );
                
                statements.Add(
                    SyntaxFactory.LocalDeclarationStatement(
                        SyntaxFactory.VariableDeclaration(SyntaxFactory.IdentifierName("var"))
                            .AddVariables(
                                SyntaxFactory.VariableDeclarator(childNodeName)
                                    .WithInitializer(SyntaxFactory.EqualsValueClause(
                                        invocation
                                    ))
                            )
                    )
                );
                
                // if (childNode != null) { parentNode.Children.Add(childNode); }
                statements.Add(
                    SyntaxFactory.IfStatement(
                        SyntaxFactory.BinaryExpression(SyntaxKind.NotEqualsExpression,
                            SyntaxFactory.IdentifierName(childNodeName),
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression)),
                        SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName(parentNode), SyntaxFactory.IdentifierName("Children")),
                                        SyntaxFactory.IdentifierName("Add")))
                                .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(childNodeName)))
                            )
                        )
                    )
                );
                break;
        }

        return statements;
    }


    private ExpressionSyntax CreateReaderExpression(IUnityTypeInfo typeInfo, string readerParamName = "reader", TypeSyntax? expectedType = null)
    {
        return typeInfo switch
        {
            PrimitiveTypeInfo primitiveTypeInfo =>
                expectedType == null 
                ? SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(readerParamName),
                        SyntaxFactory.IdentifierName(
                            Helper.GetReaderMethodName(primitiveTypeInfo.OriginalTypeName))
                    )
                )
                : SyntaxFactory.CastExpression(
                    expectedType,
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(readerParamName),
                            SyntaxFactory.IdentifierName(
                                Helper.GetReaderMethodName(primitiveTypeInfo.OriginalTypeName))
                        )
                    )
                ),
            GenericPPtrTypeInfo or
            PredefinedTypeInfo =>
                SyntaxFactory.ObjectCreationExpression(typeInfo.ToTypeSyntax())
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(readerParamName))),
            ClassTypeInfo classTypeInfo =>
                SyntaxFactory.ObjectCreationExpression(classTypeInfo.ToConcreteTypeSyntax())
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.IdentifierName(readerParamName))),
            PairTypeInfo pairTypeInfo =>
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(readerParamName),
                        SyntaxFactory.GenericName("ReadPairWithAlign")
                            .AddTypeArgumentListArguments(pairTypeInfo.Item1Type.ToTypeSyntax(), pairTypeInfo.Item2Type.ToTypeSyntax())
                    )
                ).AddArgumentListArguments(
                    SyntaxFactory.Argument(SyntaxFactory.SimpleLambdaExpression(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("r")),
                        CreateReaderExpression(pairTypeInfo.Item1Type, "r")
                    )),
                    SyntaxFactory.Argument(SyntaxFactory.SimpleLambdaExpression(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("r")),
                        CreateReaderExpression(pairTypeInfo.Item2Type, "r")
                    )),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(pairTypeInfo.Item1RequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression)),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(pairTypeInfo.Item2RequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression))
                ),
            VectorTypeInfo vectorTypeInfo =>
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(readerParamName),
                        SyntaxFactory.GenericName("ReadListWithAlign")
                            .AddTypeArgumentListArguments(vectorTypeInfo.ElementType.ToTypeSyntax())
                    )
                ).AddArgumentListArguments(
                    SyntaxFactory.Argument(SyntaxFactory.SimpleLambdaExpression(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("r")),
                        CreateReaderExpression(vectorTypeInfo.ElementType, "r")
                    )),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(vectorTypeInfo.ElementRequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression))
                ),
            MapTypeInfo mapTypeInfo =>
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(readerParamName),
                        SyntaxFactory.GenericName("ReadListWithAlign")
                            .AddTypeArgumentListArguments(mapTypeInfo.PairType.ToTypeSyntax())
                    )
                ).AddArgumentListArguments(
                    SyntaxFactory.Argument(SyntaxFactory.SimpleLambdaExpression(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("r")),
                        CreateReaderExpression(mapTypeInfo.PairType, "r")
                    )),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(mapTypeInfo.PairRequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression))
                ),
            _ => throw new Exception("Unreachable")
        };
    }

    private ConstructorDeclarationSyntax BuildConstructor(string className, List<UnityFieldInfo> fieldInfos)
    {
        var assignments = new List<ExpressionStatementSyntax>();

        foreach (var fieldInfo in fieldInfos)
        {
            assignments.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(fieldInfo.Name),
                        CreateReaderExpression(fieldInfo.TypeInfo, expectedType: fieldInfo.DeclaredTypeSyntax)
                )
            ));

            if (fieldInfo.RequireAlign)
            {
                var alignStatement = SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        // "reader.Align"
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("reader"),
                            SyntaxFactory.IdentifierName("Align")
                        )
                    )
                    .WithArgumentList(
                        // "(4)"
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(
                                    SyntaxFactory.LiteralExpression(
                                        SyntaxKind.NumericLiteralExpression,
                                        SyntaxFactory.Literal(4)
                                    )
                                )
                            )
                        )
                    )
                );
            
                assignments.Add(alignStatement);
            }
        }

        var constructor = SyntaxFactory.ConstructorDeclaration(className)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("reader")).WithType(SyntaxFactory.ParseTypeName("IReader"))
            )
            .WithBody(SyntaxFactory.Block(assignments));

        return constructor;
    }
}