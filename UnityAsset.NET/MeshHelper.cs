using UnityAsset.NET.Files;

namespace UnityAsset.NET;

public static class MeshHelper
{
    public enum VertexChannelFormat
    {
        Float,
        Float16,
        Color,
        Byte,
        UInt32
    }

    public enum VertexFormat2017
    {
        Float,
        Float16,
        Color,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }

    public enum VertexFormat
    {
        Float,
        Float16,
        UNorm8,
        SNorm8,
        UNorm16,
        SNorm16,
        UInt8,
        SInt8,
        UInt16,
        SInt16,
        UInt32,
        SInt32
    }
    
    public static VertexFormat ToVertexFormat(int format, UnityRevision version)
    {
        if (version.Major < 2019)
        {
            switch ((VertexFormat2017)format)
            {
                case VertexFormat2017.Float:
                    return VertexFormat.Float;
                case VertexFormat2017.Float16:
                    return VertexFormat.Float16;
                case VertexFormat2017.Color:
                case VertexFormat2017.UNorm8:
                    return VertexFormat.UNorm8;
                case VertexFormat2017.SNorm8:
                    return VertexFormat.SNorm8;
                case VertexFormat2017.UNorm16:
                    return VertexFormat.UNorm16;
                case VertexFormat2017.SNorm16:
                    return VertexFormat.SNorm16;
                case VertexFormat2017.UInt8:
                    return VertexFormat.UInt8;
                case VertexFormat2017.SInt8:
                    return VertexFormat.SInt8;
                case VertexFormat2017.UInt16:
                    return VertexFormat.UInt16;
                case VertexFormat2017.SInt16:
                    return VertexFormat.SInt16;
                case VertexFormat2017.UInt32:
                    return VertexFormat.UInt32;
                case VertexFormat2017.SInt32:
                    return VertexFormat.SInt32;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }
        else
        {
            return (VertexFormat)format;
        }
    }
    
    public static uint GetFormatSize(VertexFormat format)
    {
        switch (format)
        {
            case VertexFormat.Float:
            case VertexFormat.UInt32:
            case VertexFormat.SInt32:
                return 4u;
            case VertexFormat.Float16:
            case VertexFormat.UNorm16:
            case VertexFormat.SNorm16:
            case VertexFormat.UInt16:
            case VertexFormat.SInt16:
                return 2u;
            case VertexFormat.UNorm8:
            case VertexFormat.SNorm8:
            case VertexFormat.UInt8:
            case VertexFormat.SInt8:
                return 1u;
            default:
                throw new ArgumentOutOfRangeException(nameof(format), format, null);
        }
    }
}