using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace UnityAsset.NET.SourceGenerator.NodeAdapter;

/// <summary>
/// Emits the NodeData backend's adapter layer: the name-to-adapter dispatch the registry calls, one class per
/// interface, and one read/write/match triple per type the interfaces mention. Everything this generator needs is in
/// the checked-in interfaces, so it selects its input here and leaves the reading, the shape decisions and the text to
/// <see cref="InterfaceModel"/>, <see cref="NodeAdapterShape"/> and <see cref="NodeAdapterEmitter"/>.
/// </summary>
[Generator]
public sealed class NodeAdapterGenerator : IIncrementalGenerator
{
    /// <summary>The assembly whose interfaces are adapted: another consumer has no <c>TypeRegistry</c> to adapt for.</summary>
    private const string TargetAssembly = "UnityAsset.NET";

    /// <summary>
    /// The generated interface tree, plus the hand-written partials beside it. Interfaces declared anywhere else are
    /// deliberately not adapted - the runtime resolves those through their stand-in types.
    /// </summary>
    private const string InterfaceFolder = "Types/PreDefined/";

    public void Initialize(IncrementalGeneratorInitializationContext context) =>
        context.RegisterSourceOutput(context.CompilationProvider, (output, compilation) =>
        {
            if (compilation.AssemblyName != TargetAssembly) return;

            // Filter by path before touching any root: a source tree that is not read is not parsed.
            var definitions = compilation.SyntaxTrees
                .Where(tree => tree.FilePath.Replace('\\', '/').Contains(InterfaceFolder))
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                .GroupBy(declaration => declaration.Identifier.Text)
                .ToDictionary(group => group.Key, group => group.ToArray());

            output.AddSource("NodeAdapters.g.cs", new NodeAdapterEmitter(new InterfaceModel(definitions)).Emit());
        });
}
