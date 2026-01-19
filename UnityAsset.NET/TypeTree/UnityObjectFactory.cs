using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTree.PreDefined;

namespace UnityAsset.NET.TypeTree;

public static class UnityObjectFactory
{
    public static IUnityAsset Create(SerializedType sType, IReader reader)
    {
        if (sType.ToTypeName() == "MonoBehaviour")
        {
            return new PreDefined.Types.MonoBehaviour(reader, sType.Nodes);
        }
        
        var generatedType = AssemblyManager.GetType(sType);

        if (generatedType == null)
        {
            throw new Exception($"Type {sType.TypeID} not found");
        }
        var instance = Activator.CreateInstance(generatedType, args: [ reader ]);
        return (IUnityAsset)(instance ?? throw new InvalidOperationException("Activator.CreateInstance unexpectedly returned null for type: " + generatedType.FullName));
    }
}