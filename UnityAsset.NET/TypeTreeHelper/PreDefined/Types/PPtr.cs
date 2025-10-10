using System.Diagnostics.CodeAnalysis;
using System.Text;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.IO;
using UnityAsset.NET.IO.Reader;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

[OriginalName("PPtr")]
public class PPtr<T> : IPreDefinedType where T : IUnityType
{
    public string ClassName => $"PPtr<{UnityTypeHelper.GetClassName(typeof(T))}>";
    public Int32 m_FileID { get; }
    public Int64 m_PathID { get; }
    internal readonly AssetReader _reader;

    public PPtr(IReader reader)
    {
        m_FileID = reader.ReadInt32();
        m_PathID = reader.ReadInt64();
        _reader = (AssetReader)reader;
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}int m_FileID = {m_FileID}");
        sb.AppendLine($"{childIndent}SInt64 m_PathID = {m_PathID}");
        return sb;
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