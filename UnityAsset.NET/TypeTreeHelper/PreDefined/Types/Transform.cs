using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Transform : IPreDefinedType, IComponent
{
    public string ClassName => "Transform";

    public PPtr<IGameObject> m_GameObject { get; }

    public Quaternionf m_LocalRotation { get; }

    public Vector3f m_LocalPosition { get; }

    public Vector3f m_LocalScale { get; }

    public List<PPtr<Transform>> m_Children { get; }

    public PPtr<Transform> m_Father { get; }

    public Transform(IReader reader)
    {
        m_GameObject = new PPtr<IGameObject>(reader);
        m_LocalRotation = new Quaternionf(reader);
        m_LocalPosition = new Vector3f(reader);
        m_LocalScale = new Vector3f(reader);
        reader.Align(4);
        m_Children = reader.ReadListWithAlign<PPtr<Transform>>(reader.ReadInt32(), r => new PPtr<Transform>(r), false);
        reader.Align(4);
        m_Father = new PPtr<Transform>(reader);
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        this.m_GameObject?.ToPlainText("m_GameObject", sb, childIndent);
        this.m_LocalRotation?.ToPlainText("m_LocalRotation", sb, childIndent);
        this.m_LocalPosition?.ToPlainText("m_LocalPosition", sb, childIndent);
        this.m_LocalScale?.ToPlainText("m_LocalScale", sb, childIndent);
        sb.AppendLine($"{childIndent}vector m_Children");
        sb.AppendLine($"{childIndent}    Array Array");
        sb.AppendLine($"{childIndent}    int size =  {(uint)this.m_Children.Count}");
        for (int im_Children = 0; im_Children < this.m_Children.Count; im_Children++)
        {
            var m_ChildrenchildIndentBackUp = childIndent;
            childIndent = $"{childIndent}        ";
            sb.AppendLine($"{childIndent}[{im_Children}]");
            this.m_Children[im_Children]?.ToPlainText("data", sb, childIndent);
            childIndent = m_ChildrenchildIndentBackUp;
        }
        this.m_Father?.ToPlainText("m_Father", sb, childIndent);
        return sb;
    }
}