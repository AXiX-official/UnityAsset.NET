using System.IO;

namespace UnityAsset.NET.IO;

/// <summary>
/// Bounds an array before the array exists.
/// </summary>
public static class ArrayBounds
{
    public static void Count(IReader reader, int count, string what)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");

        var remaining = reader.Remaining;
        if (count > remaining)
            throw new EndOfStreamException(
                $"{what}: {count} element(s) cannot fit in the {remaining} byte(s) left " +
                $"(at position {reader.Position}).");
    }
    
    public static void Elements(IReader reader, int count, long unitSize, string what)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative.");

        if (unitSize <= 0)
            return;

        var remaining = reader.Remaining;
        if (count > remaining / unitSize)
            throw new EndOfStreamException(
                $"{what}: {count} × {unitSize} byte(s) cannot fit in the {remaining} byte(s) left " +
                $"(at position {reader.Position}).");
    }
}