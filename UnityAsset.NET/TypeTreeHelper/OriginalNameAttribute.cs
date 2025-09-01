namespace UnityAsset.NET.TypeTreeHelper;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public sealed class OriginalNameAttribute : Attribute
{
    public string Name { get; }

    public OriginalNameAttribute(string name)
    {
        Name = name;
    }
}