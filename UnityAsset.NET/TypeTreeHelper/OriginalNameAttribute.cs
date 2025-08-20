namespace UnityAsset.NET.TypeTreeHelper;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property)]
public class OriginalNameAttribute : Attribute
{
    public string Name { get; }

    public OriginalNameAttribute(string name)
    {
        Name = name;
    }
}