namespace UnityAsset.NET.Extensions;

public static class Vector
{
    private const float kEpsilon = 0.00001F;
    
    public static (float X, float Y, float Z) Normalize(this (float X, float Y, float Z) vector)
    {
        var length = vector.Length();
        if (length > kEpsilon)
        {
            var invNorm = 1.0f / length;
            return (vector.X * invNorm, vector.Y * invNorm, vector.Z * invNorm);
        }

        return (0, 0, 0);
    }
    
    public static float Length(this (float X, float Y, float Z) vector)
    {
        return (float)Math.Sqrt(vector.LengthSquared());
    }
    
    public static float LengthSquared(this (float X, float Y, float Z) vector)
    {
        return vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z;
    }
}