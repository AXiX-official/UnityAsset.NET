using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class PPtr<T> : IPPtr where T : IUnityObject
{
    private static string GetGenericClassName()
    {
        var name = typeof(T).Name;
        if (name.StartsWith("I"))
            return name.Substring(1);
        return name;
    }
    public string ClassName => $"PPtr<{GetGenericClassName()}>";
    public Int32 m_FileID { get; }
    public Int64 m_PathID { get; }
    private SerializedFile m_SerializedFile;

    public PPtr(IReader reader)
    {
        m_FileID = reader.ReadInt32();
        m_PathID = reader.ReadInt64();
        m_SerializedFile = ((AssetReader)reader).AssetsFile;
    }

    public PPtr(Int32 fileID, Int64 pathID, IReader reader)
    {
        m_FileID = fileID;
        m_PathID = pathID;
        m_SerializedFile = ((AssetReader)reader).AssetsFile;
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode
        {
            Name = name,
            TypeName = ClassName
        };
        root.Children.Add(new AssetNode { Name = "m_FileID", TypeName = "UInt32", Value = m_FileID });
        root.Children.Add(new AssetNode { Name = "m_PathID", TypeName = "UInt64", Value = m_PathID });
        return root;
    }
    
    public bool TryGet(AssetManager assetManager, [NotNullWhen(true)] out T? result)
    {
        result = default;
        if (m_PathID == 0)
            return false;
        if (TryGetAssetsFile(assetManager, out var sourceFile))
        {
            if (sourceFile.PathToAsset.TryGetValue(m_PathID, out var obj))
            {
                if (obj.Value is T variable)
                {
                    result = variable;
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool TryGetAssetsFile(AssetManager assetManager, [NotNullWhen(true)] out SerializedFile? sf)
    {
        sf = null;
        if (m_FileID == 0)
        {
            sf = m_SerializedFile;
            return true;
        }

        var externals = m_SerializedFile.Metadata.Externals;
        if (m_FileID > 0 && m_FileID - 1 < externals.Count)
        {
            var m_External = externals[m_FileID - 1];
            var fileFound = assetManager.LoadedFiles.TryGetValue(m_External.FileName, out var sfw);
            if (fileFound)
            {
                if (sfw is SerializedFile serializedFile)
                {
                    sf = serializedFile;
                    return true;
                }
            }
        }

        return false;
    }
}