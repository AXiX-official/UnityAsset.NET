using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn.IR;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn
{
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

            /*var toPlainTextMethod = BuildToPlainTextMethod(rootNode.Type, children);
            if (toPlainTextMethod != null)
            {
                members.Add(toPlainTextMethod);
            }*/

            classDeclaration = classDeclaration.AddMembers(members.ToArray());
            
            NamespaceDeclaration = NamespaceDeclaration.AddMembers(classDeclaration);

            return classDeclaration;
        }

        private ExpressionSyntax CreateReaderExpression(IUnityTypeInfo typeInfo, string readerParamName = "reader")
        {
            return typeInfo switch
            {
                PrimitiveTypeInfo primitiveTypeInfo =>
                    SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(readerParamName),
                            SyntaxFactory.IdentifierName(
                                RoslynBuilderHelper.GetReaderMethodName(primitiveTypeInfo.OriginalTypeName))
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
                var fieldName = RoslynBuilderHelper.SanitizeName(fieldInfo.Name);
                assignments.Add(SyntaxFactory.ExpressionStatement(
                    SyntaxFactory.AssignmentExpression(
                        SyntaxKind.SimpleAssignmentExpression,
                        SyntaxFactory.IdentifierName(fieldName),
                            CreateReaderExpression(fieldInfo.TypeInfo)
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

        /*private MethodDeclarationSyntax BuildToPlainTextMethod(string className, List<TypeTreeNode> children)
        {
            var statements = new List<StatementSyntax>();

            statements.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(SyntaxKind.CoalescingAssignmentExpression,
                    SyntaxFactory.IdentifierName("sb"),
                    SyntaxFactory.ObjectCreationExpression(SyntaxFactory.ParseTypeName("StringBuilder")).WithArgumentList(SyntaxFactory.ArgumentList()))
            ));

            statements.Add(SyntaxFactory.ExpressionStatement(
                SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("sb"), SyntaxFactory.IdentifierName("AppendLine")))
                .AddArgumentListArguments(SyntaxFactory.Argument(
                    SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken))
                    .AddContents(
                        SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName("indent")),
                        SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxKind.InterpolatedStringTextToken, $" {className} ")),
                        SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName("name"))
                    )
                ))
            ));

            statements.Add(
                SyntaxFactory.LocalDeclarationStatement(
                    SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName("var"))
                    .AddVariables(
                        SyntaxFactory.VariableDeclarator("childIndent")
                        .WithInitializer(SyntaxFactory.EqualsValueClause(
                            SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken))
                            .AddContents(
                                SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName("indent")),
                                SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxKind.InterpolatedStringTextToken, "\t"))
                            )
                        ))
                    )
                )
            );

            foreach (var child in children)
            {
                if (RoslynBuilderHelper.IsPrimitive(child))
                {
                    var fieldName = RoslynBuilderHelper.SanitizeName(child.Name);
                    var fieldType = RoslynBuilderHelper.GetCSharpPrimitiveType(child.Type);

                    statements.Add(SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("sb"), SyntaxFactory.IdentifierName("AppendLine")))
                        .AddArgumentListArguments(SyntaxFactory.Argument(
                            SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken))
                            .AddContents(
                                SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName("childIndent")),
                                SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxKind.InterpolatedStringTextToken, $"{fieldType} {fieldName} = ")),
                                SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName(fieldName))
                            )
                        ))
                    ));
                }
                else if (RoslynBuilderHelper.IsVector(child, children))
                {
                    var fieldName = RoslynBuilderHelper.SanitizeName(child.Name);
                    var loopItemName = $"item_{fieldName}";

                    var foreachStatement = SyntaxFactory.ForEachStatement(
                        SyntaxFactory.ParseTypeName("var"),
                        loopItemName,
                        SyntaxFactory.IdentifierName(fieldName),
                        SyntaxFactory.Block(
                            SyntaxFactory.ExpressionStatement(
                                SyntaxFactory.InvocationExpression(
                                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.IdentifierName("sb"), SyntaxFactory.IdentifierName("AppendLine")))
                                .AddArgumentListArguments(SyntaxFactory.Argument(
                                    SyntaxFactory.InterpolatedStringExpression(SyntaxFactory.Token(SyntaxKind.InterpolatedStringStartToken))
                                    .AddContents(
                                        SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName("childIndent")),
                                        SyntaxFactory.InterpolatedStringText(SyntaxFactory.Token(SyntaxKind.InterpolatedStringTextToken, "\t- ")),
                                        SyntaxFactory.Interpolation(SyntaxFactory.IdentifierName(loopItemName))
                                    )
                                ))
                            )
                        )
                    );
                    statements.Add(foreachStatement);
                }
                else if (RoslynBuilderHelper.IsGenericPPtr(child))
                {
                    var fieldName = RoslynBuilderHelper.SanitizeName(child.Name);
                    var statement = SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.ConditionalAccessExpression(
                            SyntaxFactory.IdentifierName(fieldName),
                            SyntaxFactory.InvocationExpression(
                                SyntaxFactory.MemberBindingExpression(SyntaxFactory.IdentifierName("ToPlainText")))
                            .AddArgumentListArguments(
                                SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(fieldName))),
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("sb")),
                                SyntaxFactory.Argument(SyntaxFactory.IdentifierName("childIndent"))
                            )
                        )
                    );
                    statements.Add(statement);
                }
            }

            statements.Add(SyntaxFactory.ReturnStatement(SyntaxFactory.IdentifierName("sb")));

            var methodDeclaration = SyntaxFactory.MethodDeclaration(
                SyntaxFactory.ParseTypeName("StringBuilder"), "ToPlainText")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .AddParameterListParameters(
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("name")).WithType(SyntaxFactory.ParseTypeName("string")).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal("Base")))),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("sb")).WithType(SyntaxFactory.ParseTypeName("StringBuilder?")).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))),
                    SyntaxFactory.Parameter(SyntaxFactory.Identifier("indent")).WithType(SyntaxFactory.ParseTypeName("string")).WithDefault(SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(""))))
                )
                .WithBody(SyntaxFactory.Block(statements));

            return methodDeclaration;
        }*/
    }
}