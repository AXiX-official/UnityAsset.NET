using Microsoft.CodeAnalysis.CSharp;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityAsset.NET.Extensions;
using UnityAsset.NET.Files.SerializedFiles;
using UnityAsset.NET.TypeTreeHelper.PreDefined;

namespace UnityAsset.NET.TypeTreeHelper.Compiler.Roslyn
{
    public static class RoslynBuilderHelper
    {
        public static readonly Dictionary<string, Type> PreDefinedInterfaceMap;
    
        public static readonly Dictionary<string, Type> PreDefinedTypeMap;

        static RoslynBuilderHelper()
        {
            var assembly = Assembly.GetExecutingAssembly();
        
            PreDefinedInterfaceMap = assembly
                .GetTypes()
                .Where(t => t.IsInterface && 
                            (t.Namespace == "UnityAsset.NET.TypeTreeHelper.PreDefined.Classes" || 
                             t.Namespace == "UnityAsset.NET.TypeTreeHelper.PreDefined.Interfaces"))
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        
            PreDefinedTypeMap = assembly
                .GetTypes()
                .Where(t => typeof(IPreDefinedType).IsAssignableFrom(t) && 
                            t.IsClass && !t.IsAbstract)
                .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);
        }
        
        
        #region IdentifierSanitizer Logic
        
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                throw new System.Exception("Unexpected null or empty name for type generate.");
            
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
                else
                {
                    sanitized.Append('_');
                }
            }
            var fixedName = sanitized.ToString();
            
            return SyntaxFacts.IsReservedKeyword(SyntaxFacts.GetKeywordKind(fixedName)) ? "@" + fixedName : fixedName;
        }

        #endregion

        #region Type Helper Logic

        public static string GetCSharpPrimitiveType(string unityType)
        {
            return unityType switch
            {
                "SInt8" => "sbyte",
                "UInt8" => "byte",
                "char" => "char",
                "short" or "SInt16" => "short",
                "UInt16" or "unsigned short" => "ushort",
                "int" or "SInt32" => "int",
                "UInt32" or "unsigned int" or "Type*" => "uint",
                "long long" or "SInt64" => "long",
                "UInt64" or "unsigned long long" or "FileSize" => "ulong",
                "float" => "float",
                "double" => "double",
                "bool" => "bool",
                "string" => "string",
                _ => throw new NotSupportedException($"Unknown primitive Unity type: {unityType}")
            };
        }
    
        public static bool IsPrimitive(TypeTreeNode current)
        {
            return current.Type switch
            {
                "SInt8" or "UInt8" or "char" or "short" or "SInt16" or "UInt16" or "unsigned short" or "int" or
                "SInt32" or "UInt32" or "unsigned int" or "Type*" or "long long" or "SInt64" or "UInt64" or
                "unsigned long long" or "FileSize" or "float" or "double" or "bool" or "string" => true,
                _ => false,
            };
        }

        public static string GetReaderMethodName(string unityType)
        {
            return unityType switch
            {
                "SInt8" => "ReadSByte",
                "UInt8" => "ReadByte",
                "char" => "ReadChar",
                "short" or "SInt16" => "ReadInt16",
                "UInt16" or "unsigned short" => "ReadUInt16",
                "int" or "SInt32" => "ReadInt32",
                "UInt32" or "unsigned int" or "Type*" => "ReadUInt32",
                "long long" or "SInt64" => "ReadInt64",
                "UInt64" or "unsigned long long" or "FileSize" => "ReadUInt64",
                "float" => "ReadFloat",
                "double" => "ReadDouble",
                "bool" => "ReadBoolean",
                "string" => "ReadSizedString",
                _ => throw new System.NotSupportedException($"No IReader method for Unity type: {unityType}")
            };
        }

        public static bool IsVector(TypeTreeNode current, List<TypeTreeNode> nodes)
        {
            if (current.Type == "vector") return true;
            var children = nodes.Children(current);
            if (children == null || children.Count == 0) return false;
            if (children[0].Type == "Array") return true;
            return false;
        }

        public static bool IsGenericPPtr(TypeTreeNode current)
        {
            return current.Type.StartsWith("PPtr<") && current.Type.EndsWith(">");
        }
        
        public static bool IsPreDefinedType(TypeTreeNode current) => PreDefinedTypeMap.ContainsKey(current.Type);
        
        public static bool IsMap(TypeTreeNode current)
        {
            if (current.Type == "map") return true;
            return false;
        }
    
        public static bool IsPair(TypeTreeNode current)
        {
            return current.Type == "pair";
        }

        #endregion

        #region Predefined Interface Logic

        public static bool IsNullable(Type csharpType)
        {
            if (!csharpType.IsValueType)
            {
                return true;
            }
            return csharpType.IsGenericType && csharpType.GetGenericTypeDefinition() == typeof(Nullable<>);
        }
    
        public static Dictionary<string, PropertyInfo> GetAllInterfaceProperties(Type type)
        {
            var properties = new Dictionary<string, PropertyInfo>();

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                properties[property.Name] = property;
            }

            foreach (var baseInterface in type.GetInterfaces())
            {
                foreach (var property in GetAllInterfaceProperties(baseInterface))
                {
                    properties[property.Key] = property.Value;
                }
            }

            return properties;
        }

        #endregion

        public static TypeSyntax GetTypeSyntax(Type type)
        {
            if (type.FullName == "UnityAsset.NET.TypeTreeHelper.PreDefined.Types.Object")
            {
                return SyntaxFactory.ParseTypeName("global::UnityAsset.NET.TypeTreeHelper.PreDefined.Types.Object");
            }
            
            if (type.IsGenericType)
            {
                var genericBaseName = type.Name.Split('`')[0];
                if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
                    return type.GetGenericArguments().Select(GetTypeSyntax).First();
                var typeArgumentSyntaxes = type.GetGenericArguments().Select(GetTypeSyntax);
                return SyntaxFactory.GenericName(
                    SyntaxFactory.Identifier(genericBaseName)
                )
                .WithTypeArgumentList(
                    SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SeparatedList(typeArgumentSyntaxes)
                    )
                );
            }

            return SyntaxFactory.ParseTypeName(type.Name);
        }
    }
}
