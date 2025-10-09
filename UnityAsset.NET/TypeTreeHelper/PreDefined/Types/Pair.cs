using System.Text;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Pair<TFirst, TSecond> : IPreDefinedType where TFirst : IUnityType where TSecond : IUnityType
{
    public string ClassName => "pair";
    
    public TFirst first { get; }
    public TSecond second { get; }
    private Action<string, StringBuilder, string, TFirst> FirstToPlainText { get; }
    private Action<string, StringBuilder, string, TSecond> SecondToPlainText { get; }

    public Pair(TFirst first, TSecond second, Action<string, StringBuilder, string, TFirst> firstToPlainText, Action<string, StringBuilder, string, TSecond> secondToPlainText)
    {
        this.first = first;
        this.second = second;
        FirstToPlainText = firstToPlainText;
        SecondToPlainText = secondToPlainText;
    }

    public StringBuilder ToPlainText(string name = "Base", StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        sb.AppendLine($"{indent}{ClassName} {name}");
        var childIndent = indent + "    ";
        FirstToPlainText("first", sb, childIndent, first);
        SecondToPlainText("second", sb, childIndent, second);
        return sb;
    }
}