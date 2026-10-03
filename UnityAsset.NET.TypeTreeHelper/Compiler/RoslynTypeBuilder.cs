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
        
        if (typeInfo.DeclaresNamedObject)
        {
            classDeclaration = classDeclaration.AddBaseListTypes(
                SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName("INamedObject")));
        }

        var classNameProperty = SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("string"), "ClassName")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(typeInfo.Name))))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));


        classDeclaration = classDeclaration.AddMembers(classNameProperty);
        
        var members = new List<MemberDeclarationSyntax>();

        foreach (var fieldInfo in typeInfo.Fields)
        {
            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(
                    fieldInfo.DeclaredTypeSyntax,
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

        if (BuildWriteMethod(typeInfo) is { } writeMethod)
            members.Add(writeMethod);
        members.Add(toAssetNodeMethod);

        classDeclaration = classDeclaration.AddMembers(members.ToArray());
        
        NamespaceDeclaration = NamespaceDeclaration.AddMembers(classDeclaration);

        return classDeclaration;
    }
    
    private static bool CanWrite(UnityFieldInfo field)
    {
        if (!CanWriteDeclaration(field.DeclaredTypeSyntax))
            return false;
        
        var declaredIsArray = field.DeclaredTypeSyntax switch
        {
            ArrayTypeSyntax => true,
            NullableTypeSyntax { ElementType: ArrayTypeSyntax } => true,
            _ => false,
        };
        var shapeIsArray = field.TypeInfo is VectorTypeInfo or MapTypeInfo;

        return declaredIsArray == shapeIsArray && CanWrite(field.TypeInfo);
    }

    /// <summary>
    /// Whether a declaration can be written. A plain "T?" can. A RefSum&lt;...&gt; can when every alternative is a PPtr,
    /// because a PPtr always knows how to write itself; other unions wait, since their alternatives would have to be
    /// checked the same way and none of them may be written yet.
    /// </summary>
    private static bool CanWriteDeclaration(TypeSyntax declaration)
    {
        if (declaration is NullableTypeSyntax nullable)
            return CanWriteDeclaration(nullable.ElementType);

        if (declaration is not GenericNameSyntax { Identifier.Text: "RefSum" } union)
            return true;

        return union.TypeArgumentList.Arguments.All(alternative =>
            UnionAlternativeMethod(alternative.ToString()) is not null);
    }

    /// <summary>
    /// The method that writes one alternative of a union, or null when that alternative has no encoder yet. Both the
    /// guard that admits a union and the code that writes it ask this, so what is admitted is exactly what can be
    /// written: a stand-in or a reference writes itself, and a leaf is written by the writer method for its type.
    /// </summary>
    private static string? UnionAlternativeMethod(string alternativeType)
    {
        if (alternativeType.Contains("PPtr<", StringComparison.Ordinal))
            return "Write";

        return PrimitiveWriterMethods.TryGetValue(alternativeType, out var method) ? method : null;
    }

    // The C# names a union can name an alternative by, and the writer method that writes one.
    private static readonly Dictionary<string, string> PrimitiveWriterMethods = new()
    {
        ["bool"] = "WriteBoolean",
        ["byte"] = "WriteByte",
        ["sbyte"] = "WriteSByte",
        ["char"] = "WriteChar",
        ["short"] = "WriteInt16",
        ["ushort"] = "WriteUInt16",
        ["int"] = "WriteInt32",
        ["uint"] = "WriteUInt32",
        ["long"] = "WriteInt64",
        ["ulong"] = "WriteUInt64",
        ["float"] = "WriteSingle",
        ["double"] = "WriteDouble",
        ["string"] = "WriteSizedString",
    };

    /// <summary>
    /// Whether a shape can be written back: the leaf kinds, the hand-written stand-ins, an array whose elements can, and
    /// a nested class that can be written itself. That last case asks the nested class's own fields the same
    /// field-level question its own guard asks, so "has an encoder" and "can be delegated to" are one thing.
    /// </summary>
    private static bool CanWrite(IUnityTypeInfo typeInfo)
        => typeInfo switch
        {
            PrimitiveTypeInfo or PredefinedTypeInfo or GenericPPtrTypeInfo => true,
            VectorTypeInfo vector => CanWrite(vector.ElementType),
            MapTypeInfo map => CanWrite(map.PairType),
            PairTypeInfo pair => CanWrite(pair.Item1Type) && CanWrite(pair.Item2Type),
            ClassTypeInfo nested => nested.Fields.All(CanWrite),
            _ => false,
        };

    /// <summary>
    /// The expression that writes one value back, mirroring the reader's: a leaf writes itself, a stand-in writes itself
    /// through its own encoder, and an array writes its elements with the writer counterpart of the call the reader
    /// used. Null when the shape has no encoder.
    /// </summary>
    private static ExpressionSyntax? CreateWriterExpression(IUnityTypeInfo typeInfo, string access, string writerParam,
        TypeSyntax? expectedType = null)
    {
        if (typeInfo is VectorTypeInfo vector)
        {
            if (CreateWriterExpression(vector.ElementType, "item", "w") is not { } elementWriter)
                return null;

            // The element lambda takes the writer and the item, so it is a parenthesized lambda rather than the simple
            // one the reader side gets away with.
            return SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(writerParam),
                        SyntaxFactory.GenericName("WriteArrayWithAlign")
                            .AddTypeArgumentListArguments(vector.ElementType.ToTypeSyntax())))
                .AddArgumentListArguments(
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(access)),
                    SyntaxFactory.Argument(SyntaxFactory.ParenthesizedLambdaExpression(
                        SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("w")),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("item")),
                        })),
                        elementWriter)),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(vector.ElementRequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression)));
        }

        return typeInfo switch
        {
            PrimitiveTypeInfo primitive => SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(writerParam),
                        SyntaxFactory.IdentifierName(Helper.GetWriterMethodName(primitive.OriginalTypeName))))
                .AddArgumentListArguments(SyntaxFactory.Argument(
                    expectedType == null
                        ? SyntaxFactory.IdentifierName(access)
                        : SyntaxFactory.CastExpression(expectedType, SyntaxFactory.IdentifierName(access)))),

            // The declared type decides what the writer is handed, and the cast goes the opposite way from the reader's:
            // the reader reads the shape's type and casts the result to the declaration, while the writer is handed the
            // declaration and casts it to the shape's type, which is what its method accepts.

            // A map is read as an array of pairs, so it is written as the writer's counterpart of that same call.
            MapTypeInfo map when CreateWriterExpression(map.PairType, "item", "w") is { } pairWriter =>
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(writerParam),
                        SyntaxFactory.GenericName("WriteArrayWithAlign")
                            .AddTypeArgumentListArguments(map.PairType.ToTypeSyntax())))
                .AddArgumentListArguments(
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(access)),
                    SyntaxFactory.Argument(SyntaxFactory.ParenthesizedLambdaExpression(
                        SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("w")),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("item")),
                        })),
                        pairWriter)),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(map.PairRequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression))),

            // A pair is read key first, then value, with an alignment after each: the writer's call takes the value and
            // the two element writers in that same order.
            PairTypeInfo pair
                when CreateWriterExpression(pair.Item1Type, "k", "w") is { } keyWriter
                     && CreateWriterExpression(pair.Item2Type, "v", "w") is { } valueWriter =>
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(writerParam),
                        SyntaxFactory.GenericName("WritePairWithAlign")
                            .AddTypeArgumentListArguments(
                                pair.Item1Type.ToTypeSyntax(), pair.Item2Type.ToTypeSyntax())))
                .AddArgumentListArguments(
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(access)),
                    SyntaxFactory.Argument(SyntaxFactory.ParenthesizedLambdaExpression(
                        SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("w")),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("k")),
                        })),
                        keyWriter)),
                    SyntaxFactory.Argument(SyntaxFactory.ParenthesizedLambdaExpression(
                        SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList(new[]
                        {
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("w")),
                            SyntaxFactory.Parameter(SyntaxFactory.Identifier("v")),
                        })),
                        valueWriter)),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(pair.Item1RequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression)),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(pair.Item2RequireAlign
                        ? SyntaxKind.TrueLiteralExpression
                        : SyntaxKind.FalseLiteralExpression))),

            // A nested class is declared as its interface, and an interface declares no Write, so the field is cast to
            // the generated class that does - the same class the reader constructs.
            ClassTypeInfo nested => SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParenthesizedExpression(
                        SyntaxFactory.CastExpression(
                            SyntaxFactory.ParseTypeName(nested.GeneratedClassName),
                            SyntaxFactory.IdentifierName(access))),
                    SyntaxFactory.IdentifierName("Write")))
                .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(writerParam))),

            // A stand-in is a class in its own right and encodes itself, exactly as the reader calls its constructor.
            PredefinedTypeInfo or GenericPPtrTypeInfo => SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(access),
                        SyntaxFactory.IdentifierName("Write")))
                .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(writerParam))),

            _ => null,
        };
    }

    private MethodDeclarationSyntax? BuildWriteMethod(ClassTypeInfo typeInfo)
    {
        if (typeInfo.Fields.Any(field => !CanWrite(field)))
            return null;

        var statements = new List<StatementSyntax>();

        foreach (var field in typeInfo.Fields)
        {
            // A member declared "T?" is a Nullable<T> only when T is a value type; otherwise the "?" is an annotation
            // and the member is read and written directly. A string is a reference type, and so are the stand-ins that
            // are classes.
            var isNullableValueType = field.DeclaredTypeSyntax is NullableTypeSyntax
                && field.TypeInfo switch
                {
                    PrimitiveTypeInfo primitive => primitive.OriginalTypeName != "string",
                    PairTypeInfo => true,
                    PredefinedTypeInfo predefined =>
                        Helper.PreDefinedStructs.Contains(predefined.PredefinedTypeSyntax.ToString()),
                    _ => false,
                };

            // The declared type decides what the writer is handed, with any nullable wrapper taken off, and the value is
            // cast to the shape's own type where the two differ - an int in the tree declared as uint.
            var declaredUnderlying = field.DeclaredTypeSyntax is NullableTypeSyntax nullableDeclaration
                ? nullableDeclaration.ElementType
                : field.DeclaredTypeSyntax;
            var leafType = field.TypeInfo.ToTypeSyntax();
            var needsCast = declaredUnderlying.ToString() != leafType.ToString();

            // A RefSum<...> declaration means the versions disagree about this member's type. Which alternative this
            // class has is not inferred from the payload's runtime type - a PPtr<IUnityObject> fallback would match many
            // of them - but taken from the index the reader stored when it chose, through the union's own Switch, whose
            // branches therefore line up with the reader's by construction and whose default throws rather than guesses.
            // A RefSum is a struct, so RefSum<...>? is a Nullable and the union is reached through .Value.
            var unionSyntax = field.DeclaredTypeSyntax is NullableTypeSyntax { ElementType: GenericNameSyntax u } ? u
                : field.DeclaredTypeSyntax as GenericNameSyntax;
            if (unionSyntax is { Identifier.Text: "RefSum" })
            {
                var unionAccess = field.DeclaredTypeSyntax is NullableTypeSyntax ? $"{field.Name}.Value" : field.Name;
                statements.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(unionAccess),
                            SyntaxFactory.IdentifierName("Switch")))
                    .AddArgumentListArguments(unionSyntax.TypeArgumentList.Arguments
                        .Select((alternative, index) =>
                        {
                            var parameter = $"v{index}";
                            var method = UnionAlternativeMethod(alternative.ToString())!;

                            // "Write" means the alternative writes itself; anything else is a writer method that takes
                            // the value, which is how a leaf is written.
                            ExpressionSyntax body = method == "Write"
                                ? SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName(parameter),
                                        SyntaxFactory.IdentifierName("Write")))
                                    .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName("writer")))
                                : SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        SyntaxFactory.IdentifierName("writer"),
                                        SyntaxFactory.IdentifierName(method)))
                                    .AddArgumentListArguments(SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter)));

                            return SyntaxFactory.Argument(SyntaxFactory.SimpleLambdaExpression(
                                SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameter)), body));
                        })
                        .ToArray())));
                continue;
            }

            // The same helper answers "can this be written" and produces the expression, so the guard above and the
            // encoder below cannot drift apart.
            statements.Add(SyntaxFactory.ExpressionStatement(
                CreateWriterExpression(field.TypeInfo,
                    isNullableValueType ? $"{field.Name}.Value" : field.Name,
                    "writer",
                    needsCast ? leafType : null)!));

            // The reader aligns after the field it read, so the writer pads after writing it.
            if (field.RequireAlign)
            {
                statements.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName("writer"),
                            SyntaxFactory.IdentifierName("Align")))
                    .AddArgumentListArguments(
                        SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                            SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(4))))));
            }
        }

        return SyntaxFactory.MethodDeclaration(
                SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)), "Write")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddParameterListParameters(
                SyntaxFactory.Parameter(SyntaxFactory.Identifier("writer"))
                    .WithType(SyntaxFactory.ParseTypeName("IWriter")))
            .WithBody(SyntaxFactory.Block(statements));
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
                                                    SyntaxFactory.Literal(typeInfo.TypeTreeRepr.TypeName)
                                                )
                                            )
                                    )
                                )
                            ))
                    )
            )
        );

        var names = new LocalNames();
        names.Reserve("rootAssetNode");

        foreach (var fieldInfo in typeInfo.Fields)
        {
            statements.AddRange(CreateAssetNodeCreationStatement(fieldInfo.Name, LocalPath.Root(fieldInfo.Name), fieldInfo.TypeInfo, "rootAssetNode", names, fieldInfo.NeedsNullGuard, fieldInfo.DeclaredTypeSyntax));
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

    private IEnumerable<StatementSyntax> CreateAssetNodeCreationStatement(string valueExpression, LocalPath path, IUnityTypeInfo typeInfo, string parentNode, LocalNames names, bool isOptional = false, TypeSyntax? declaredTypeSyntax = null)
    {
        var statements = new List<StatementSyntax>();
        var node = typeInfo.TypeTreeRepr;
        var valueAccess = SyntaxFactory.ParseExpression(valueExpression);

        // The declared type is what the interface says, so an optional member arrives as "T?": the shape checks below
        // are about the underlying type.
        if (declaredTypeSyntax is NullableTypeSyntax nullableDeclaredType)
            declaredTypeSyntax = nullableDeclaredType.ElementType;

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
                            valueAccess
                        )
                    )
                    : valueAccess;
                
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
                var vectorNodeName = names.Declare(path.VectorNode);

                bool isSumType = declaredTypeSyntax is GenericNameSyntax { Identifier.Text: "RefSum" };
                
                ExpressionSyntax vectorTargetExpression = isSumType
                    ? SyntaxFactory.ParenthesizedExpression(
                        SyntaxFactory.CastExpression(
                            /*SyntaxFactory.GenericName("List")
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
                                }),*/
                            SyntaxFactory.ArrayType(v.ElementType switch
                                {
                                    PrimitiveTypeInfo p => p.ToTypeSyntax(),
                                    VectorTypeInfo ve => ve.ToTypeSyntax(),
                                    MapTypeInfo m => m.ToTypeSyntax(),
                                    PairTypeInfo pa => pa.ToTypeSyntax(),
                                    ClassTypeInfo c => SyntaxFactory.ParseTypeName(c.InterfaceName),
                                    PredefinedTypeInfo pd => pd.PredefinedTypeSyntax,
                                    GenericPPtrTypeInfo gp => gp.ToTypeSyntax(),
                                    _ => throw new Exception("Unreachable")
                                })
                                .AddRankSpecifiers(
                                    SyntaxFactory.ArrayRankSpecifier(
                                        SyntaxFactory.SingletonSeparatedList<ExpressionSyntax>(
                                            SyntaxFactory.OmittedArraySizeExpression()
                                        )
                                    )
                                ),
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                valueAccess,
                                SyntaxFactory.IdentifierName("Value")
                            )
                        )
                    )
                    : valueAccess;
                
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
                var itemIdentifier = names.Declare(path.Item);
                var foreachStatement = SyntaxFactory.ForEachStatement(SyntaxFactory.IdentifierName("var"), itemIdentifier, vectorTargetExpression,
                    SyntaxFactory.Block(CreateAssetNodeCreationStatement(itemIdentifier, path.Item, v.ElementType, vectorNodeName, names))
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
                var mapNodeName = names.Declare(path.MapNode);
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
                var mapPairName = names.Declare(path.Pair);
                var mapForeach = SyntaxFactory.ForEachStatement(SyntaxFactory.IdentifierName("var"), mapPairName, valueAccess,
                    SyntaxFactory.Block(CreateAssetNodeCreationStatement(mapPairName, path.Pair, m.PairType, mapNodeName, names))
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
                 var pairNodeName = names.Declare(path.PairNode);
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
                statements.AddRange(CreateAssetNodeCreationStatement($"{valueExpression}.Item1", path.Item1, p.Item1Type, pairNodeName, names));
                statements.AddRange(CreateAssetNodeCreationStatement($"{valueExpression}.Item2", path.Item2, p.Item2Type, pairNodeName, names));

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
                var childNodeName = names.Declare(path.ChildNode);
                // var childNode = this.FieldName?.ToAssetNode("FieldName");

                bool needValueAccess = declaredTypeSyntax is GenericNameSyntax { Identifier.Text: "RefSum" };
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
                            // A RefSum<...>? is a Nullable<RefSum<...>>, and there ".Value" unwraps the Nullable and
                            // hands back the union itself, which is not what this cast expects. The payload has to be
                            // reached through the lifted access, which is also the null-safe one.
                            ? isOptional
                                ? SyntaxFactory.ConditionalAccessExpression(
                                        valueAccess,
                                        SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("Value")))
                                : SyntaxFactory.MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        valueAccess,
                                        SyntaxFactory.IdentifierName("Value")
                                )
                            : valueAccess
                        )
                    )
                    : valueAccess;
                
                ExpressionSyntax invocation = isOptional
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
                        SyntaxFactory.GenericName("ReadArrayWithAlign")
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
                        SyntaxFactory.GenericName("ReadArrayWithAlign")
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