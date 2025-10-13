using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined;

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
        var instance = Activator.CreateInstance(generatedType, args: [ reader ]);
        return (IAsset)(instance ?? throw new InvalidOperationException("Activator.CreateInstance unexpectedly returned null for type: " + generatedType.FullName));
    }
}