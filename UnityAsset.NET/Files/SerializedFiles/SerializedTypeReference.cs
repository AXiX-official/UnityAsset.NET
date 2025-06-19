using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.Files.SerializedFiles;

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
    
    public static SerializedTypeReference Parse(IReader reader) => new (
        reader.ReadNullTerminatedString(),
        reader.ReadNullTerminatedString(),
        reader.ReadNullTerminatedString()
    );

    public void Serialize(IWriter writer)
    {
        writer.WriteNullTerminatedString(ClassName);
        writer.WriteNullTerminatedString(Namespace);
        writer.WriteNullTerminatedString(AsmName);
    }
    
    public long SerializeSize => 3 + ClassName.Length + Namespace.Length + AsmName.Length;

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