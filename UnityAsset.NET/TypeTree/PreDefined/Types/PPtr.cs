using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTree.PreDefined.Interfaces;

namespace UnityAsset.NET.TypeTree.PreDefined.Types;

public class PPtr<T> : IPPtr where T : IUnityType
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
    internal readonly AssetReader _reader;

    public PPtr(IReader reader)
    {
        m_FileID = reader.ReadInt32();
        m_PathID = reader.ReadInt64();
        _reader = (AssetReader)reader;
    }

    public PPtr(Int32 fileID, Int64 pathID, IReader reader)
    {
        m_FileID = fileID;
        m_PathID = pathID;
        _reader = (AssetReader)reader;
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
    
    public bool TryGet([NotNullWhen(true)] out T? result)
    {
        result = default;
        if (TryGetAssetsFile(out var sourceFile))
        {
            var index = sourceFile.Assets.FindIndex(a => a.Info.PathId == m_PathID);
            if ( index != -1)
            {
                var obj = sourceFile.Assets[index];
                if (obj.Value is T variable)
                {
                    result = variable;
                    return true;
                }
            }
        }
        
        return false;
    }

    private bool TryGetAssetsFile([NotNullWhen(true)] out SerializedFile? sf)
    {
        sf = null;
        if (m_FileID == 0)
        {
            sf = _reader.AssetsFile;
            return true;
        }
        
        if (m_FileID > 0 && m_FileID - 1 < _reader.AssetsFile.Metadata.Externals.Count)
        {
            throw new NotImplementedException();
        }

        return false;
    }
}