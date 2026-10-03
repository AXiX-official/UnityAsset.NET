using System.Text;
using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityAsset.NET.TypeTreeHelper;
using UnityAsset.NET.TypeTreeHelper.Compiler;
using UnityAsset.NET.TypeTreeHelper.Diagnostics;
using UnityAsset.NET.TypeTreeHelper.Model;
using UnityAsset.NET.TypeTreeHelper.Schema;

namespace UnityAsset.NET.UnityTypeGen;

public class InterfaceGenerator
{
    private Dictionary<string, List<(UnityVersion, TypeTreeRepr)>> _subNodes = new();

    public static string RootClassFolderName { get; set; } = "Classes";
    public static string SubClassFolderName { get; set; } = "Interfaces";

    /// <summary>
    /// Where to write the interface schema table for the runtime generator; null generates no table.
    /// </summary>
    public static string? SchemaTablePath { get; set; }
    
    private readonly Dictionary<string, HashSet<string>> _referencedInterfaces = new();
    private readonly HashSet<string> _generatedInterfaces = new();

    // Hand-written interfaces that have no generated counterpart. A hand-written partial sharing a name with a
    // generated interface extends it instead, and is already in _generatedInterfaces.
    private static readonly HashSet<string> HandWrittenInterfaces =
    [
        "INamedObject", "IUnityAsset", "IUnityObject", "IPreDefinedInterface", "IPreDefinedObject",
        "IAnimationCurve", "IKeyframe", "IMonoBehaviour", "IAnimatorController", "IAnimatorOverrideController",
        "IRuntimeAnimatorController", "Renderer",
    ];

    /// <summary>
    /// The base interfaces of generated ones that the type trees do not state, because a hand-written partial used to
    /// declare them. Generation emits them now, so the hierarchy is stated once, in the generated output.
    /// </summary>
    private static readonly Dictionary<string, string[]> DeclaredBaseInterfaces = new(StringComparer.Ordinal)
    {
        ["IAnimation"] = ["IComponent"],
        ["IAnimator"] = ["IComponent"],
        ["IMeshFilter"] = ["IComponent"],
        ["IMeshRenderer"] = ["IComponent", "Renderer"],
        ["ISkinnedMeshRenderer"] = ["IComponent", "Renderer"],
        ["ITransform"] = ["IComponent"],
    };
    
    public void GenerateInterfaces(string outputDirectory, Dictionary<string, List<(UnityVersion, TypeTreeRepr)>> rootTypeNodesMap)
    {
        ValidateNamedFields(rootTypeNodesMap);
        _referencedInterfaces.Clear();
        _generatedInterfaces.Clear();

        if (!Directory.Exists(outputDirectory))
            Directory.CreateDirectory(outputDirectory);
        var rootClassDir = Path.Combine(outputDirectory, RootClassFolderName);
        var subClassDir = Path.Combine(outputDirectory, SubClassFolderName);

        // Nothing on disk is touched until every interface has been generated and checked - see the end of this method.
        // Cleaning up first and validating last is what left the checked-in interfaces half rewritten when a run
        // reported a problem: the old files were gone and the new ones were already in place.
        
        // A class whose name a nested structure already owns gets no interface of its own; the structure keeps it.
        var includedRootTypes = rootTypeNodesMap.Where(kvp =>
            !Helper.ExcludedTypes.Contains(kvp.Key) && !Helper.ClassInterfacesTakenByNestedTypes.Contains(kvp.Key)
            && (Helper.IncludedTypes.Contains(kvp.Key) || Helper.IncludedPPTrGenricTypes.Contains(kvp.Key))
        ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        
        var excludedRootTypes = rootTypeNodesMap.Where(kvp =>
            !includedRootTypes.ContainsKey(kvp.Key)
        ).Select(kvp => kvp.Value);
        
        DiscoverAllSubNodes(includedRootTypes.Values, out var subNodes);
        DiscoverLeftSubNodes(excludedRootTypes, subNodes);
        
        _subNodes = subNodes.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Select(v => (v.Value, v.Key)).ToList()
        );
        
        var schemas = new Dictionary<string, UnityTypeSchema>(StringComparer.Ordinal);
        foreach (var (className, rootNodes) in includedRootTypes)
            schemas[$"I{className}"] = UnitySchemaBuilder.Build(className, rootNodes);
        foreach (var (className, subNodeList) in _subNodes)
            schemas[$"I{className}"] = UnitySchemaBuilder.Build(className, subNodeList);

        var schemaEntries = new Dictionary<string, (string BaseInterface, UnityTypeSchema Schema)>(StringComparer.Ordinal);

        // Everything is generated and checked before anything on disk is touched, so a run that reports a problem
        // leaves the working tree exactly as it found it.
        var pending = new List<(string InterfaceName, string Source, bool IsRoot)>();

        foreach (var (className, _) in includedRootTypes)
        {
            var interfaceName = $"I{className}";
            var schema = schemas[interfaceName];
            var compilationUnit = GenerateClassInterface(schema, true, InheritedMembers(interfaceName, schemas), out var baseInterface);
            schemaEntries[interfaceName] = (baseInterface, schema);

            pending.Add((interfaceName, compilationUnit.NormalizeWhitespace(elasticTrivia: true).ToFullString(), true));
            _generatedInterfaces.Add(interfaceName);
        }

        foreach (var (className, _) in _subNodes)
        {
            var interfaceName = $"I{className}";
            var schema = schemas[interfaceName];
            var subCompilationUnit = GenerateClassInterface(schema, false, InheritedMembers(interfaceName, schemas), out var baseInterface);
            schemaEntries[interfaceName] = (baseInterface, schema);

            pending.Add((interfaceName, subCompilationUnit.NormalizeWhitespace(elasticTrivia: true).ToFullString(), false));
            _generatedInterfaces.Add(interfaceName);
        }

        ValidateReferencedInterfaces();
        _diagnostics.ThrowIfAny();

        // Only now are the old interfaces removed and the new ones written.
        if (Directory.Exists(outputDirectory))
        {
            foreach (var file in new DirectoryInfo(outputDirectory).GetFiles())
                file.Delete();
            foreach (var dir in new DirectoryInfo(outputDirectory).GetDirectories())
                dir.Delete(true);
        }

        Directory.CreateDirectory(rootClassDir);
        Directory.CreateDirectory(subClassDir);

        foreach (var (interfaceName, source, isRoot) in pending)
            File.WriteAllText(Path.Combine(isRoot ? rootClassDir : subClassDir, $"{interfaceName}.g.cs"), source);

        if (SchemaTablePath is { } schemaTablePath)
        {
            WriteSchemaTable(schemaEntries, schemaTablePath);
            WriteVersionTable(schemas, Path.Combine(Path.GetDirectoryName(schemaTablePath)!, "InterfaceVersionTable.g.cs"));
        }
    }

    /// <summary>
    /// Writes the per-version member sets of the interfaces that have more than one known version. An interface with a
    /// single version needs no entry: its declared members are that version's members. Nothing reads this yet - it is
    /// the hook that version-specific behaviour (ADR-0002) defers to.
    /// </summary>
    private static void WriteVersionTable(Dictionary<string, UnityTypeSchema> schemas, string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// Warning: This file is auto-generated. Do not edit manually.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("namespace UnityAsset.NET.TypeTreeHelper.Schema;");
        builder.AppendLine();
        builder.AppendLine("public static class InterfaceVersionTable");
        builder.AppendLine("{");
        builder.AppendLine("    public static readonly IReadOnlyDictionary<string, IReadOnlyList<InterfaceVersionSchema>> ByInterfaceName =");
        builder.AppendLine("        new Dictionary<string, IReadOnlyList<InterfaceVersionSchema>>(StringComparer.Ordinal)");
        builder.AppendLine("    {");

        foreach (var (interfaceName, schema) in schemas.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (schema.Versions.Count < 2)
                continue;

            builder.AppendLine($"        [\"{interfaceName}\"] = new InterfaceVersionSchema[]");
            builder.AppendLine("        {");

            foreach (var version in schema.Versions)
            {
                builder.AppendLine($"            new(\"{version.UnityVersion}\", new InterfaceVersionMember[]");
                builder.AppendLine("            {");

                foreach (var member in version.Members
                             .Where(member => !(schema.IsNamedAsset && member.Name == "m_Name"))
                             .OrderBy(member => Helper.SanitizeName(member.Name), StringComparer.Ordinal))
                    builder.AppendLine($"                new(\"{Helper.SanitizeName(member.Name)}\", \"{member.ResolvedType}\"),");

                builder.AppendLine("            }),");
            }

            builder.AppendLine("        },");
        }

        builder.AppendLine("    };");
        builder.AppendLine("}");
        File.WriteAllText(path, builder.ToString());
    }

    /// <summary>
    /// Writes what the emitted interfaces say, for the runtime generator to read instead of reflecting over them. The
    /// declared types are copied verbatim, so a version-optional member keeps the "?" the interface declares it with.
    /// </summary>
    private static void WriteSchemaTable(
        Dictionary<string, (string BaseInterface, UnityTypeSchema Schema)> entries,
        string path)
    {
        var derivesFromNamedObject = new Dictionary<string, bool>(StringComparer.Ordinal);

        bool DerivesFromNamedObject(string interfaceName)
        {
            if (derivesFromNamedObject.TryGetValue(interfaceName, out var known))
                return known;

            var result = false;
            if (entries.TryGetValue(interfaceName, out var entry))
            {
                var baseInterface = entry.BaseInterface;
                result = baseInterface == "INamedObject"
                         || (entries.ContainsKey(baseInterface) && DerivesFromNamedObject(baseInterface))
                         || (HandWrittenInterfaceSchema.Entries.TryGetValue(baseInterface, out var handWritten)
                             && handWritten.DerivesFromNamedObject);
            }

            derivesFromNamedObject[interfaceName] = result;
            return result;
        }

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// Warning: This file is auto-generated. Do not edit manually.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine();
        builder.AppendLine("namespace UnityAsset.NET.TypeTreeHelper.Schema;");
        builder.AppendLine();
        builder.AppendLine("/// <summary>");
        builder.AppendLine("/// What the interfaces generation emitted say, keyed by interface name. The generator writes this file; run the");
        builder.AppendLine("/// UnityTypeGen project to regenerate it.");
        builder.AppendLine("/// </summary>");
        builder.AppendLine("internal static class GeneratedInterfaceSchemaTable");
        builder.AppendLine("{");
        builder.AppendLine("    public static readonly IReadOnlyDictionary<string, InterfaceSchema> Entries =");
        builder.AppendLine("        new Dictionary<string, InterfaceSchema>(StringComparer.Ordinal)");
        builder.AppendLine("    {");

        foreach (var (interfaceName, entry) in entries.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"        [\"{interfaceName}\"] = new InterfaceSchema(\"{interfaceName}\", "
                + $"{(DerivesFromNamedObject(interfaceName) ? "true" : "false")}, new InterfaceMemberSchema[]");
            builder.AppendLine("        {");

            // The runtime looks a member up by the name the interface spells it with, so the table records that name,
            // and it keeps the declared type verbatim: a version-optional member keeps its "?".
            foreach (var member in entry.Schema.Members()
                         .OrderBy(member => Helper.SanitizeName(member.Name), StringComparer.Ordinal))
            {
                var name = Helper.SanitizeName(member.Name);
                var declaredType = member.IsVersionOptional ? $"{member.ResolvedType}?" : member.ResolvedType;
                builder.AppendLine($"            new(\"{name}\", \"{declaredType}\", "
                    + $"{(member.IsVersionOptional ? "true" : "false")}),");
            }

            builder.AppendLine("        }),");
        }

        builder.AppendLine("    };");
        builder.AppendLine("}");

        File.WriteAllText(path, builder.ToString());
    }

    private static readonly DiagnosticBag _diagnostics = new();

    private void RecordReferences(string interfaceName, string typeName)
    {
        foreach (Match match in Regex.Matches(typeName, @"\bI[A-Za-z_][A-Za-z0-9_]*\b"))
        {
            if (!_referencedInterfaces.TryGetValue(match.Value, out var owners))
                _referencedInterfaces[match.Value] = owners = new HashSet<string>();
            owners.Add(interfaceName);
        }
    }

    private void ValidateReferencedInterfaces()
    {
        foreach (var (referenced, owners) in _referencedInterfaces)
        {
            if (_generatedInterfaces.Contains(referenced) || HandWrittenInterfaces.Contains(referenced))
                continue;

            _diagnostics.Report(
                DiagnosticCodes.InterfaceNotGenerated,
                $"{referenced} is referenced but generation did not emit it and it is not hand-written.",
                new DiagnosticLocation(
                    string.Join(", ", owners.OrderBy(owner => owner, StringComparer.Ordinal)),
                    MemberPath: referenced));
        }
    }
    
    private void DiscoverSubNodesRecursive(TypeTreeRepr node, in Dictionary<string, Dictionary<TypeTreeRepr, UnityVersion>> subNodes, UnityVersion version)
    {
        foreach (var subNode in node.SubNodes)
        {
            var type = subNode.TypeName;
        
            if (type.StartsWith("PPtr<"))
                type = "PPtr";
            
            if (Helper.IsPrimitive(type))
                continue;
            
            if (!Helper.ExcludedBasicTypes.Contains(type) && !Helper.NoInterfaceTypes.Contains(type) && !Helper.ExcludedTypes.Contains(type))
            {
                if (!subNodes.TryGetValue(type, out var dict))
                {
                    subNodes[type] = dict = new();
                }

                if (dict.TryGetValue(subNode, out var otherVersion))
                {
                    if (version < otherVersion)
                        dict[subNode] = version;
                }
                else
                {
                    dict[subNode] = version;
                }
            }

            DiscoverSubNodesRecursive(subNode, subNodes, version);
        }
    }
    
    private void DiscoverLeftSubNodesRecursive(TypeTreeRepr node, Dictionary<string, Dictionary<TypeTreeRepr, UnityVersion>> subNodes, UnityVersion version, bool forceAdd = false)
    {
        foreach (var subNode in node.SubNodes)
        {
            var type = subNode.TypeName;
        
            if (type.StartsWith("PPtr<"))
                type = "PPtr";
            
            if (Helper.IsPrimitive(type))
                continue;
            
            if (subNodes.TryGetValue(type, out var dict))
            {
                if (dict.TryGetValue(subNode, out var otherVersion))
                {
                    if (version < otherVersion)
                        dict[subNode] = version;
                }
                else
                {
                    dict[subNode] = version;
                }
                forceAdd = true;
            }
            else if (forceAdd)
            {
                if (!Helper.ExcludedBasicTypes.Contains(type) && !Helper.NoInterfaceTypes.Contains(type) && !Helper.ExcludedTypes.Contains(type))
                {
                    subNodes[type] = dict = new();
                    if (dict.TryGetValue(subNode, out var otherVersion))
                    {
                        if (version < otherVersion)
                            dict[subNode] = version;
                    }
                    else
                    {
                        dict[subNode] = version;
                    }
                }
            }
            
            DiscoverLeftSubNodesRecursive(subNode, subNodes, version, forceAdd);
        }
    }
    
    private void DiscoverLeftSubNodes(IEnumerable<IEnumerable<(UnityVersion, TypeTreeRepr)>> allNodes, Dictionary<string, Dictionary<TypeTreeRepr, UnityVersion>> subNodes)
    {
        foreach (var nodes in allNodes)
        foreach (var (version, rootNode) in nodes)
        {
            DiscoverLeftSubNodesRecursive(rootNode, subNodes, version);
        }
    }

    private void DiscoverAllSubNodes(IEnumerable<IEnumerable<(UnityVersion, TypeTreeRepr)>> allNodes, out Dictionary<string, Dictionary<TypeTreeRepr, UnityVersion>> subNodes)
    {
        subNodes = new();
    
        foreach (var nodes in allNodes)
        foreach (var (version, rootNode) in nodes)
        {
            DiscoverSubNodesRecursive(rootNode, subNodes, version);
        }
    }

    private static void ValidateNamedFields(Dictionary<string, List<(UnityVersion, TypeTreeRepr)>> rootTypeNodesMap)
    {
        foreach (var (className, nodes) in rootTypeNodesMap)
        foreach (var (version, node) in nodes)
            ValidateNamedFields(className, version, node);
    }

    private static void ValidateNamedFields(string className, UnityVersion version, TypeTreeRepr node)
    {
        foreach (var child in node.SubNodes)
        {
            if (child.Name == "m_Name" && child.TypeName != "string")
                _diagnostics.Report(
                    DiagnosticCodes.NamedMemberIsNotString,
                    $"{node.TypeName}.{child.Name} is {child.TypeName}, not a string, so the class cannot carry a name.",
                    new DiagnosticLocation(className, version.ToString(), $"{node.TypeName}.{child.Name}"));

            ValidateNamedFields(className, version, child);
        }
    }

    /// <summary>The member names an interface inherits, so a declaration that hides one can say so.</summary>
    private static HashSet<string> InheritedMembers(string interfaceName, Dictionary<string, UnityTypeSchema> schemas)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        if (!DeclaredBaseInterfaces.TryGetValue(interfaceName, out var declaredBaseInterfaces))
            return names;

        foreach (var baseInterface in declaredBaseInterfaces)
            Add(baseInterface, names);

        return names;

        void Add(string name, HashSet<string> into)
        {
            if (schemas.TryGetValue(name, out var baseSchema))
                foreach (var member in baseSchema.Members())
                    into.Add(Helper.SanitizeName(member.Name));
            else if (HandWrittenInterfaceSchema.Entries.TryGetValue(name, out var handWritten))
                foreach (var member in handWritten.Members)
                    into.Add(member.Name);

            if (DeclaredBaseInterfaces.TryGetValue(name, out var deeper))
                foreach (var baseInterface in deeper)
                    Add(baseInterface, into);
        }
    }

    private CompilationUnitSyntax GenerateClassInterface(UnityTypeSchema schema, bool isRootClass, HashSet<string> inheritedMembers, out string baseInterface)
    {
        var className = schema.Name;

        var usingDirectives = SyntaxFactory.List([
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("UnityAsset.NET.Types.PreDefined.Types"))
        ]);
        
        var namespaceDeclaration = SyntaxFactory.NamespaceDeclaration(
            SyntaxFactory.ParseName("UnityAsset.NET.Types.PreDefined.Interfaces")
            );
        
        baseInterface = isRootClass ? (schema.IsNamedAsset ? "INamedObject" : "IUnityAsset") : "IPreDefinedInterface";

        var baseInterfaces = new List<string> { baseInterface };
        if (DeclaredBaseInterfaces.TryGetValue($"I{className}", out var declaredBaseInterfaces))
            baseInterfaces.AddRange(declaredBaseInterfaces);

        foreach (var name in baseInterfaces)
            RecordReferences($"I{className}", name);

        var interfaceDeclaration = SyntaxFactory.InterfaceDeclaration($"I{className}")
            .AddModifiers(
                SyntaxFactory.Token(SyntaxKind.PublicKeyword),
                SyntaxFactory.Token(SyntaxKind.PartialKeyword))
            .AddBaseListTypes(baseInterfaces
                .Select(name => (BaseTypeSyntax)SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName(name)))
                .ToArray());
        
        var members = new List<MemberDeclarationSyntax>();

        // The members and their types come from the model: the rules that resolve a type tree to a C# type live there
        // and nowhere else.
        foreach (var member in schema.Members())
        {
            var memberName = Helper.SanitizeName(member.Name);
            var typeName = member.IsVersionOptional ? $"{member.ResolvedType}?" : member.ResolvedType;
            RecordReferences($"I{className}", member.ResolvedType);

            var modifiers = new List<SyntaxToken> { SyntaxFactory.Token(SyntaxKind.PublicKeyword) };
            if (inheritedMembers.Contains(memberName))
                modifiers.Add(SyntaxFactory.Token(SyntaxKind.NewKeyword));

            var propertyDeclaration = SyntaxFactory.PropertyDeclaration(
                    SyntaxFactory.ParseTypeName(typeName), memberName)
                .AddModifiers(modifiers.ToArray());
            
            if (member.IsVersionOptional)
            {
                var getterWithBody = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block(
                        SyntaxFactory.ReturnStatement(
                            SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))));
                propertyDeclaration = propertyDeclaration.AddAccessorListAccessors(getterWithBody);
            }
            else
            {
                var abstractGetter = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                        "throw new System.NotSupportedException(\"This member has no implementation.\");")));
                propertyDeclaration = propertyDeclaration.AddAccessorListAccessors(abstractGetter);
            }
            
            // Versions lacking the member keep a rejecting setter; generated concrete members implement it normally.
            propertyDeclaration = propertyDeclaration.AddAccessorListAccessors(
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithBody(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                        "throw new System.NotSupportedException(\"This member is not editable in this version.\");"))));
            members.Add(propertyDeclaration);
        }
        
        interfaceDeclaration = interfaceDeclaration.AddMembers(members.ToArray());
        
        var leadingTrivia = SyntaxFactory.TriviaList(
            SyntaxFactory.Comment("// <auto-generated>"),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.Comment("// Warning: This file is auto-generated. Do not edit manually."),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.Comment("// </auto-generated>"),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.CarriageReturnLineFeed,
    
            SyntaxFactory.Trivia(SyntaxFactory.NullableDirectiveTrivia(SyntaxFactory.Token(SyntaxKind.EnableKeyword), true)),
            SyntaxFactory.CarriageReturnLineFeed,
            SyntaxFactory.CarriageReturnLineFeed
        );
        
        return SyntaxFactory.CompilationUnit()
            .WithUsings(usingDirectives)
            .AddMembers(namespaceDeclaration.AddMembers(interfaceDeclaration))
            .WithLeadingTrivia(leadingTrivia);
    }
}
