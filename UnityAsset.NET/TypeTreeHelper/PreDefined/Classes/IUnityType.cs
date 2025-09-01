using System.Text;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

public interface IUnityType
{
    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "");
    
    public string ClassName { get; }
}