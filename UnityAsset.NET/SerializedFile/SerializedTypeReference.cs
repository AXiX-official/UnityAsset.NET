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
    
    public void ReadMetadata(AssetReader reader)
    {
        ClassName = reader.ReadNullTerminated();
        Namespace = reader.ReadNullTerminated();
        AsmName = reader.ReadNullTerminated();
    }
}