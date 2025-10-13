using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace UnityAsset.NET.TypeTreeHelper.Compiler;

public static class IdentifierSanitizer
{
    public static bool IsKeyword(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        
        var kind = SyntaxFacts.GetKeywordKind(word);
        return kind != SyntaxKind.None;
    }
    
    public static bool IsReservedKeyword(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        
        var kind = SyntaxFacts.GetKeywordKind(word);
        return kind != SyntaxKind.None && 
               SyntaxFacts.IsReservedKeyword(kind);
    }
    
    public static bool IsContextualKeyword(string word)
    {
        if (string.IsNullOrEmpty(word))
            return false;
        
        var kind = SyntaxFacts.GetKeywordKind(word);
        return kind != SyntaxKind.None && 
               SyntaxFacts.IsContextualKeyword(kind);
    }
    
    public static string SanitizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new Exception("Unexpected null or empty name for type generate.");
        
        var sanitized = new StringBuilder(name.Length);

        if (char.IsDigit(name[0]))
        {
            sanitized.Append('_');
        }

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                sanitized.Append(c);
            }
            else if (c == '$')
            {
                continue;
            }
            else
            {
                sanitized.Append('_');
            }
        }
        var fixedName = sanitized.ToString();
        if (IsReservedKeyword(fixedName) || IsContextualKeyword(fixedName))
        {
            return "@" + fixedName;
        }
        return fixedName;
    }
}
