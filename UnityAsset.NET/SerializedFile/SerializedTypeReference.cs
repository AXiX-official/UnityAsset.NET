using System.Text;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.SerializedFile;

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
    
    public void ReadMetadata(AssetReader r)
    {
        ClassName = r.ReadNullTerminated();
        Namespace = r.ReadNullTerminated();
        AsmName = r.ReadNullTerminated();
    }

    public void Write(AssetWriter w)
    {
        w.WriteStringToNull(ClassName);
        w.WriteStringToNull(Namespace);
        w.WriteStringToNull(AsmName);
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