using System.Text.RegularExpressions;

namespace UnityAsset.NET.Files;

public class UnityRevision : IComparable<UnityRevision>, IEquatable<UnityRevision>
{
    public uint Major { get; }
    public uint Minor { get; }
    public uint Patch { get; }
    public string Extra { get; }
    
    private static readonly Regex VersionRegex = new Regex(
        @"^(?<major>\d+)(?:\.(?<minor>\d+))?(?:\.(?<patch>\d+))?(?<extra>.*)", 
        RegexOptions.Compiled);
    
    public UnityRevision(uint major, uint minor, uint patch, string extra = "")
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        Extra = extra;
    }
    
    public UnityRevision(string versionString)
    {
        var match = VersionRegex.Match(versionString);
        if (!match.Success)
            throw new FormatException($"Invalid Unity version format: {versionString}");

        Major = uint.Parse(match.Groups["major"].Value);
        Minor = match.Groups["minor"].Success ? uint.Parse(match.Groups["minor"].Value) : 0;
        Patch = match.Groups["patch"].Success ? uint.Parse(match.Groups["patch"].Value) : 0;
        Extra = match.Groups["extra"].Value;
    }
    
    public static implicit operator UnityRevision(string versionString)
    {
        return new UnityRevision(versionString);
    }
    
    public int CompareTo(UnityRevision? other)
    {
        if (other is null) return 1;
        
        int majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0) return majorComparison;
        
        int minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0) return minorComparison;
        
        return Patch.CompareTo(other.Patch); 
    }
    
    public bool Equals(UnityRevision? other)
    {
        if (other is null) return false;
        return Major == other.Major && 
               Minor == other.Minor && 
               Patch == other.Patch;
    }
    
    public override bool Equals(object? obj) => Equals(obj as UnityRevision);
    
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + Major.GetHashCode();
            hash = hash * 23 + Minor.GetHashCode();
            hash = hash * 23 + Patch.GetHashCode();
            hash = hash * 23 + Extra.ToLowerInvariant().GetHashCode();
            return hash;
        }
    }
    
    public override string ToString()
    {
        return $"{Major}.{Minor}.{Patch}{Extra}";
    }
    
    public static bool operator ==(UnityRevision left, UnityRevision right) => Equals(left, right);
    public static bool operator !=(UnityRevision left, UnityRevision right) => !Equals(left, right);
    public static bool operator <(UnityRevision left, UnityRevision right) => left.CompareTo(right) < 0;
    public static bool operator >(UnityRevision left, UnityRevision right) => left.CompareTo(right) > 0;
    public static bool operator <=(UnityRevision left, UnityRevision right) => left.CompareTo(right) <= 0;
    public static bool operator >=(UnityRevision left, UnityRevision right) => left.CompareTo(right) >= 0;
}