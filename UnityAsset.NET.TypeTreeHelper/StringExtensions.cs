using System.Security.Cryptography;
using System.Text;

namespace UnityAsset.NET.TypeTreeHelper;

public static class StringExtensions
{
    public static ulong GetStableHashCode(this string text)
    {
        using var sha256 = SHA256.Create();
        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        byte[] hashBytes = sha256.ComputeHash(textBytes);
        return BitConverter.ToUInt64(hashBytes, 0);
    }
    
    public static int GetDeterministicHashCode(this string str)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in str)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }
}