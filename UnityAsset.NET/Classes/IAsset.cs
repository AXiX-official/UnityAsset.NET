using System.Text;

namespace UnityAsset.NET.Classes;

public interface IAsset
{
    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "");
    
    public string ClassName { get; }
}