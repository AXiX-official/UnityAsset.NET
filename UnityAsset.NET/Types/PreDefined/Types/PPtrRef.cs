using UnityAsset.NET.IO;

namespace UnityAsset.NET.Types.PreDefined.Types;

/// <summary>
/// A pointer as the node tree holds it: the two scalars a <c>PPtr</c> is, without the target's type. The adapters turn one
/// into a <c>PPtr&lt;T&gt;</c> when an interface asks for it, which is why reading a pointer no longer costs a byte round
/// trip through a writer - the tree already carries exactly what the wire carries.
/// </summary>
public sealed class PPtrRef : IUnityObject
{
    public PPtrRef(int fileID, long pathID)
    {
        m_FileID = fileID;
        m_PathID = pathID;
    }

    public PPtrRef(IReader reader)
    {
        m_FileID = reader.ReadInt32();
        m_PathID = reader.ReadInt64();
    }

    public string ClassName => "PPtr";
    public int m_FileID { get; set; }
    public long m_PathID { get; set; }

    public void Write(IWriter writer)
    {
        writer.WriteInt32(m_FileID);
        writer.WriteInt64(m_PathID);
    }

    public AssetNode? ToAssetNode(string name = "Base")
    {
        var root = new AssetNode { Name = name, TypeName = ClassName };
        root.Children.Add(new AssetNode { Name = "m_FileID", TypeName = "UInt32", Value = m_FileID });
        root.Children.Add(new AssetNode { Name = "m_PathID", TypeName = "UInt64", Value = m_PathID });
        return root;
    }
}
