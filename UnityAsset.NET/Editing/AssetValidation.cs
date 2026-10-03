using System.Collections;
using System.Reflection;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.Types.PreDefined;
using UnityAsset.NET.Types.PreDefined.Interfaces;
using UnityAsset.NET.TypeTreeHelper;

namespace UnityAsset.NET.Editing;

internal static class AssetValidation
{
    public static void References(IUnityAsset value, SerializedFile file, IReadOnlySet<long> pathIds)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void Validate(int fileId, long pathId)
        {
            if (fileId < 0 || fileId > file.Metadata.Externals.Length)
                throw new InvalidOperationException($"Reference uses undeclared external FileID {fileId}.");
            if (fileId == 0 && pathId != 0 && !pathIds.Contains(pathId))
                throw new InvalidOperationException($"Reference targets missing local PathID {pathId}.");
        }
        void Visit(object? item)
        {
            if (item is null || item is string or TypeTreeRepr || item.GetType().IsPrimitive || item.GetType().IsEnum) return;
            if (!seen.Add(item)) return;
            if (item is IPPtr pointer)
            {
                Validate(pointer.m_FileID, pointer.m_PathID);
                return;
            }
            if (item is NodeData node && node.Type.StartsWith("PPtr<", StringComparison.Ordinal))
            {
                var fields = node.As<Dictionary<string, NodeData>>();
                Validate(fields["m_FileID"].As<int>(), fields["m_PathID"].As<long>());
                return;
            }
            if (item is IDictionary dictionary)
            {
                foreach (DictionaryEntry pair in dictionary) { Visit(pair.Key); Visit(pair.Value); }
                return;
            }
            if (item is IEnumerable sequence)
            {
                foreach (var element in sequence) Visit(element);
                return;
            }
            for (var type = item.GetType(); type != null; type = type.BaseType)
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    Visit(field.GetValue(item));
        }
        Visit(value);
    }
}
