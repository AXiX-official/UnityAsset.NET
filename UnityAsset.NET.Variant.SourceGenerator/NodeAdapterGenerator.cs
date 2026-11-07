using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.Variant.SourceGenerator;

[Generator]
public sealed class NodeAdapterGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(context.CompilationProvider, (output, compilation) =>
        {
            if (compilation.AssemblyName != "UnityAsset.NET") return;
            var definitions = compilation.SyntaxTrees.SelectMany(t => t.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                .Where(i => i.SyntaxTree.FilePath.Replace('\\', '/').Contains("Types/PreDefined/"))
                .GroupBy(i => i.Identifier.Text).ToDictionary(g => g.Key, g => g.ToArray());
            var generator = new Builder(definitions);
            output.AddSource("NodeAdapters.g.cs", generator.Build());
        });

    private sealed class Builder(Dictionary<string, InterfaceDeclarationSyntax[]> definitions)
    {
        private readonly Dictionary<string, int> ids = new();
        private readonly List<string> pending = new();
        private readonly StringBuilder code = new("#nullable enable\nusing System;\nusing System.Linq;\nusing UnityAsset.NET.IO;\nusing UnityAsset.NET.TypeTreeHelper;\nusing UnityAsset.NET.Types.PreDefined;\nusing UnityAsset.NET.Types.PreDefined.Types;\nusing UnityAsset.NET.Types.PreDefined.Interfaces;\nnamespace UnityAsset.NET.Types;\ninternal static class NodeAdapters {\n");
        private int Id(string type)
        {
            type = SyntaxFactory.ParseTypeName(type).WithoutTrivia().ToString();
            if (ids.TryGetValue(type, out var id)) return id;
            id = ids.Count; ids.Add(type, id); pending.Add(type); return id;
        }
        public string Build()
        {
            code.Append("internal static IUnityAsset Create(string name, object v, TypeTreeRepr s, IReader r) => name switch {\n");
            foreach (var entry in definitions.OrderBy(x => x.Key))
            {
                if (entry.Value.Any(x => x.TypeParameterList != null) || entry.Key is "IUnityObject" or "IUnityAsset" or "IPreDefinedInterface" or "IPreDefinedObject" or "INamedObject" or "Renderer") continue;
                var id = Id(entry.Key);
                code.Append('"').Append(entry.Key.StartsWith("I") ? entry.Key.Substring(1) : entry.Key).Append("\" => new Adapter").Append(id).Append("(v,s,r),\n");
            }
            code.Append("_ => s.IsNamed ? new NamedNodeObject(v,s,r) : new NodeObject(v,s,r) };\n");
            for (var index = 0; index < pending.Count; index++) Emit(pending[index], index);
            return code.Append("}\n").ToString();
        }
        private IEnumerable<(string Name, string Type)> Properties(string type)
        {
            var syntax = SyntaxFactory.ParseTypeName(type);
            var name = syntax is GenericNameSyntax g ? g.Identifier.Text : type;
            if (!definitions.TryGetValue(name, out var parts)) yield break;
            foreach (var part in parts)
            {
                var parameters = part.TypeParameterList?.Parameters.Select(x => x.Identifier.Text).ToArray() ?? Array.Empty<string>();
                var args = syntax is GenericNameSyntax gen ? gen.TypeArgumentList.Arguments.Select(x => x.ToString()).ToArray() : Array.Empty<string>();
                string Substitute(TypeSyntax t)
                {
                    var result = t;
                    foreach (var pair in parameters.Zip(args, (p, a) => (p, a)))
                        result = result.ReplaceNodes(result.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(x => x.Identifier.Text == pair.p), (n, _) => SyntaxFactory.ParseTypeName(pair.a));
                    return result.ToString();
                }
                if (part.BaseList != null)
                    foreach (var b in part.BaseList.Types)
                        foreach (var prop in Properties(Substitute(b.Type))) yield return prop;
                foreach (var prop in part.Members.OfType<PropertyDeclarationSyntax>())
                    if (prop.Identifier.ValueText != "ClassName") yield return (prop.Identifier.ValueText, Substitute(prop.Type));
            }
        }
        private void Emit(string type, int id)
        {
            var syntax = SyntaxFactory.ParseTypeName(type);
            string read, write, match;
            if (syntax is NullableTypeSyntax nullable)
            {
                var inner = nullable.ElementType.ToString(); var n = Id(inner);
                read = $"Read{n}(v,s,r)";
                write = $"Write{n}(({inner})v!,s)";
                match = $"Match{n}(s)";
            }
            else if (syntax is ArrayTypeSyntax array)
            {
                // Removing one rank handles jagged arrays.
                var inner = array.RankSpecifiers.Count == 1 ? array.ElementType.ToString() : array.WithRankSpecifiers(array.RankSpecifiers.RemoveAt(0)).ToString();
                var n = Id(inner);
                read = $"NodeObject.Elements(v).Select(x => Read{n}(x,NodeObject.Element(s),r)).ToArray()";
                write = $"NodeObject.ArrayValue(v.Select(x => Write{n}(x,NodeObject.Element(s))).ToArray(),s)";
                match = $"s.SubNodes.Length == 1 && s.SubNodes[0].TypeName == \"Array\" && Match{n}(NodeObject.Element(s))";
            }
            else if (syntax is GenericNameSyntax generic && generic.Identifier.Text == "RefSum")
            {
                var args = generic.TypeArgumentList.Arguments.Select(x => x.ToString()).ToArray();
                var ns = args.Select(Id).ToArray();
                read = string.Concat(ns.Select((n, i) => (n, i)).OrderBy(x => args[x.i].Contains("PPtr<IUnityObject>") ? 1 : 0).Select(x => $"Match{x.n}(s) ? new {type}({x.i},Read{x.n}(v,s,r)) : ")) + "throw new NotSupportedException($\"No variant matches {s.TypeName}.\")";
                write = string.Concat(ns.Select((n, i) => $"v.Value is {args[i]} a{i} ? Write{n}(a{i},s) : ")) + "throw new NotSupportedException(\"Invalid variant.\")";
                match = string.Join(" || ", ns.Select(n => $"Match{n}(s)"));
            }
            else if (syntax is GenericNameSyntax tuple && tuple.Identifier.Text == "ValueTuple")
            {
                var a = Id(tuple.TypeArgumentList.Arguments[0].ToString()); var b = Id(tuple.TypeArgumentList.Arguments[1].ToString());
                read = $"new {type}(Read{a}(NodeObject.PairItem(v,\"first\"),s.SubNodes[0],r),Read{b}(NodeObject.PairItem(v,\"second\"),s.SubNodes[1],r))";
                write = $"NodeObject.Pair(Write{a}(v.Item1,s.SubNodes[0]),Write{b}(v.Item2,s.SubNodes[1]),s)";
                match = "s.TypeName == \"pair\"";
            }
            else
            {
                var name = syntax is GenericNameSyntax g ? g.Identifier.Text : type;
                if (definitions.ContainsKey(name))
                {
                    read = $"new Adapter{id}(v,s,r)"; write = "NodeObject.WriteConcrete(v,s)";
                    var properties = Properties(type).GroupBy(x => x.Name).Select(x => x.First()).ToArray();
                    code.Append($"private sealed class Adapter{id}(object v, TypeTreeRepr s, IReader r) : NodeObject(v,s,r), {type} {{\n");
                    foreach (var prop in properties)
                    {
                        var n = Id(prop.Type);
                        code.Append($"public {prop.Type} @{prop.Name} {{ get => Get<{prop.Type}>(\"{prop.Name}\",Read{n},Write{n}); set => Set(\"{prop.Name}\",value,Write{n}); }}\n");
                    }
                    code.Append("}\n");
                }
                else if (syntax is PredefinedTypeSyntax || name == "TypelessData")
                { read = $"({type})v"; write = "v"; }
                else
                { read = $"NodeObject.ReadConcrete(v,s,r,x => new {type}(x))"; write = "NodeObject.WriteConcrete(v,s)"; }
                var unityName = name.StartsWith("I") && definitions.ContainsKey(name) ? name.Substring(1) : type.Replace(" ", "");
                if (name == "PPtr") unityName = "PPtr<" + (((GenericNameSyntax)syntax).TypeArgumentList.Arguments[0].ToString() == "IUnityObject" ? "Object" : ((GenericNameSyntax)syntax).TypeArgumentList.Arguments[0].ToString().TrimStart('I')) + ">";
                var names = name switch {
                    "sbyte" => new[]{"SInt8"}, "byte" => new[]{"UInt8"}, "short" => new[]{"short","SInt16"}, "ushort" => new[]{"UInt16","unsigned short"},
                    "int" => new[]{"int","SInt32"}, "uint" => new[]{"UInt32","unsigned int","Type*"}, "long" => new[]{"SInt64","long long"}, "ulong" => new[]{"UInt64","unsigned long long","FileSize"},
                    _ => new[]{unityName.Split('<')[0]} };
                match = type == "PPtr<IUnityObject>" ? "s.TypeName.StartsWith(\"PPtr<\", StringComparison.Ordinal)" : name == "PPtr" ? $"s.TypeName == \"{unityName}\"" : string.Join(" || ", names.Select(x => $"s.TypeName == \"{x}\""));
            }
            // Interface schemas unify signed/unsigned integers of equal width. Preserve their bits,
            // and restore the schema's original signedness when writing NodeData.
            var integers = new Dictionary<string, (string Other, string[] Names)> {
                ["byte"] = ("sbyte", new[]{"SInt8"}), ["sbyte"] = ("byte", new[]{"UInt8"}),
                ["ushort"] = ("short", new[]{"short","SInt16"}), ["short"] = ("ushort", new[]{"UInt16","unsigned short"}),
                ["uint"] = ("int", new[]{"int","SInt32"}), ["int"] = ("uint", new[]{"UInt32","unsigned int","Type*"}),
                ["ulong"] = ("long", new[]{"SInt64","long long"}), ["long"] = ("ulong", new[]{"UInt64","unsigned long long","FileSize"}) };
            if (integers.TryGetValue(type, out var numeric))
            {
                var alternate = string.Join(" || ", numeric.Names.Select(n => $"s.TypeName == \"{n}\""));
                read = $"v is {numeric.Other} n ? unchecked(({type})n) : ({type})v";
                write = $"({alternate}) ? (object)unchecked(({numeric.Other})v) : v";
                match = $"({match}) || ({alternate})";
            }
            code.Append($"private static {type} Read{id}(object v, TypeTreeRepr s, IReader r) => {read};\n");
            code.Append($"private static object Write{id}({type} v, TypeTreeRepr s) => {write};\n");
            code.Append($"private static bool Match{id}(TypeTreeRepr s) => {match};\n");
        }
    }
}
