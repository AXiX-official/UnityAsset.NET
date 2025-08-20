using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;

namespace UnityAsset.NET.TypeTreeHelper;

public static class UnityObjectFactory
{
    public static IAsset Create(SerializedType sType, IReader reader)
    {
        var generatedType = AssemblyManager.GetType(sType);

        if (generatedType == null)
        {
            throw new Exception($"Type {sType.TypeID} not found");
        }
        
        return (IAsset)Activator.CreateInstance(generatedType, args: new object[] { reader })!;
    }
}