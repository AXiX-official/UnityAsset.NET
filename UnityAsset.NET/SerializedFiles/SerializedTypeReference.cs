using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFiles;

public class SerializedTypeReference
{
    public string ClassName;

    public string Namespace;
    
    public string AsmName;
    
    public SerializedTypeReference() { }
    
    public SerializedTypeReference(string className, string nameSpace, string asmName)
    {
        ClassName = className;
        Namespace = nameSpace;
        AsmName = asmName;
    }
    
    public static SerializedTypeReference ParseFromReader(AssetReader reader) => new (
        reader.ReadStringToNull(),
        reader.ReadStringToNull(),
        reader.ReadStringToNull()
    );

    public void Serialize(AssetWriter writer)
    {
        writer.WriteStringToNull(ClassName);
        writer.WriteStringToNull(Namespace);
        writer.WriteStringToNull(AsmName);
    }

    public override string ToString()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Serialized Type Reference:");
        sb.AppendLine($"Class Name: {ClassName}");
        sb.AppendLine($"Namespace: {Namespace}");
        sb.AppendLine($"Assembly Name: {AsmName}");
        return sb.ToString();
    }
}