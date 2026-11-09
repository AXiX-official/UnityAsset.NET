using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

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

    /// <summary>
    /// Set by the project when the NodeData backend is selected (and implied by PublishAot). A runtime-codegen build
    /// does not need this layer, and emitting it there would put 10,000 lines of adapters into an assembly that cannot
    /// reach them.
    /// </summary>
    private const string NodeDataProperty = "build_property.UnityAssetNodeData";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var input = context.CompilationProvider.Combine(context.AnalyzerConfigOptionsProvider);
        context.RegisterSourceOutput(input, (output, pair) =>
        {
            var (compilation, options) = pair;
            if (compilation.AssemblyName != TargetAssembly) return;
            if (!IsNodeDataEnabled(options)) return;

            // Filter by path before touching any root: a source tree that is not read is not parsed.
            var definitions = compilation.SyntaxTrees
                .Where(tree => tree.FilePath.Replace('\\', '/').Contains(InterfaceFolder))
                .SelectMany(tree => tree.GetRoot().DescendantNodes().OfType<InterfaceDeclarationSyntax>())
                .GroupBy(declaration => declaration.Identifier.Text)
                .ToDictionary(group => group.Key, group => group.ToArray());

            output.AddSource("NodeAdapters.g.cs", new NodeAdapterEmitter(new InterfaceModel(definitions)).Emit());
        });
    }

    private static bool IsNodeDataEnabled(AnalyzerConfigOptionsProvider options) =>
        options.GlobalOptions.TryGetValue(NodeDataProperty, out var value)
        && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}
