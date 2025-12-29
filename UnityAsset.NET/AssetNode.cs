using System.Text;

namespace UnityAsset.NET;

public class AssetNode
{
    public required string Name { get; init; }
    public required string TypeName { get; init; }
    public object? Value { get; set; } = null;
    public List<AssetNode> Children { get; } = new();

    // TODO: its just a workaround
    public void ToPlainText(StringBuilder sb, int indentLevel = 0)
    {
        sb.AppendLine();
        sb.Append(' ', indentLevel);
        sb.Append($"{TypeName} {Name}: {Value?.ToString()}");
        foreach (var child in Children)
        {
            child.ToPlainText(sb, indentLevel + 4);
        }
    }
}
