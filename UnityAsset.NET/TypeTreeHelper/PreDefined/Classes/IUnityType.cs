using System.Text;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IUnityType
{
    public string ClassName { get; }
    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "");
}