﻿using System.Text;
using UnityAsset.NET.IO;
using UnityAsset.NET.TypeTreeHelper.PreDefined.Classes;

namespace UnityAsset.NET.TypeTreeHelper.PreDefined.Types;

public class Vector3f  : IPreDefinedType
{
    public string ClassName => "Vector3f";
    public float x { get; }
    public float y { get; }
    public float z { get; }

    public Vector3f(IReader reader)
    {
        x = reader.ReadFloat();
        y = reader.ReadFloat();
        z = reader.ReadFloat();
    }

    public StringBuilder ToPlainText(StringBuilder? sb = null, string indent = "")
    {
        sb ??= new StringBuilder();
        var childIndent = indent + "    ";
        sb.AppendLine($"{childIndent}float x = {x}");
        sb.AppendLine($"{childIndent}float y = {y}");
        sb.AppendLine($"{childIndent}float z = {z}");
        return sb;
    }
}