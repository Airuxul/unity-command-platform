using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Turns the loose type names a caller types (<c>"Rigidbody"</c>, <c>"UnityEngine.Rigidbody"</c>,
    /// <c>"TMPro.TextMeshProUGUI"</c>) into a <see cref="Type"/>. Ambiguous simple names fail with
    /// the full candidate list rather than picking one silently.
    /// </summary>
    public static class ComponentTypeResolver
    {
        static Dictionary<string, Type> _componentsByFullName;
        static Dictionary<string, List<Type>> _componentsBySimpleName;
        static Dictionary<string, Type> _objectsByFullName;
        static Dictionary<string, List<Type>> _objectsBySimpleName;

        public static Type RequireComponent(string name)
        {
            EnsureComponentIndex();
            return Lookup(name, _componentsByFullName, _componentsBySimpleName, typeof(Component), "component");
        }

        public static bool TryResolveComponent(string name, out Type type)
        {
            EnsureComponentIndex();
            return TryLookup(name, _componentsByFullName, _componentsBySimpleName, typeof(Component), out type);
        }

        /// <summary>Any <see cref="UnityEngine.Object"/> subtype — assets, components, ScriptableObjects.</summary>
        public static bool TryResolveObject(string name, out Type type)
        {
            EnsureObjectIndex();
            return TryLookup(name, _objectsByFullName, _objectsBySimpleName, typeof(UnityEngine.Object), out type);
        }

        static Type Lookup(
            string name,
            Dictionary<string, Type> byFullName,
            Dictionary<string, List<Type>> bySimpleName,
            Type baseType,
            string kind)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException($"A {kind} type name is required.");

            if (TryLookup(name, byFullName, bySimpleName, baseType, out var resolved))
                return resolved;

            var trimmed = name.Trim();
            if (bySimpleName.TryGetValue(trimmed, out var ambiguous) && ambiguous.Count > 1)
            {
                throw new ArgumentException(
                    $"'{trimmed}' is ambiguous. Use a full name: {string.Join(", ", ambiguous.Select(t => t.FullName).Take(10))}.");
            }

            var suggestions = bySimpleName.Keys
                .Where(k => k.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(k => k.Length)
                .Take(8)
                .ToArray();

            throw new UcpException(
                UcpErrorCodes.TypeNotFound,
                $"Unknown {kind} type '{name}'.",
                suggestions.Length > 0 ? $"Did you mean: {string.Join(", ", suggestions)}?" : null);
        }

        static bool TryLookup(
            string name,
            Dictionary<string, Type> byFullName,
            Dictionary<string, List<Type>> bySimpleName,
            Type baseType,
            out Type type)
        {
            type = null;
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var trimmed = name.Trim();

            if (byFullName.TryGetValue(trimmed, out type))
                return true;

            // Assembly-qualified names bypass the index entirely.
            var direct = Type.GetType(trimmed, throwOnError: false, ignoreCase: true);
            if (direct != null && baseType.IsAssignableFrom(direct))
            {
                type = direct;
                return true;
            }

            if (bySimpleName.TryGetValue(trimmed, out var candidates) && candidates.Count == 1)
            {
                type = candidates[0];
                return true;
            }

            return false;
        }

        static void EnsureComponentIndex()
        {
            if (_componentsByFullName != null)
                return;

            Build(TypeCache.GetTypesDerivedFrom<Component>(), out _componentsByFullName, out _componentsBySimpleName);
        }

        static void EnsureObjectIndex()
        {
            if (_objectsByFullName != null)
                return;

            Build(
                TypeCache.GetTypesDerivedFrom<UnityEngine.Object>(),
                out _objectsByFullName,
                out _objectsBySimpleName);
        }

        static void Build(
            IEnumerable<Type> types,
            out Dictionary<string, Type> byFullName,
            out Dictionary<string, List<Type>> bySimpleName)
        {
            byFullName = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            bySimpleName = new Dictionary<string, List<Type>>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract || type.IsGenericTypeDefinition || string.IsNullOrEmpty(type.FullName))
                    continue;

                byFullName[type.FullName] = type;

                if (!bySimpleName.TryGetValue(type.Name, out var list))
                {
                    list = new List<Type>();
                    bySimpleName[type.Name] = list;
                }

                list.Add(type);
            }

            // A UnityEngine.* type always wins its simple name — "Light" should never be some
            // third-party Light just because it also exists in the project.
            foreach (var list in bySimpleName.Values.Where(l => l.Count > 1))
            {
                var engineTypes = list
                    .Where(t => t.Namespace != null && t.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal))
                    .ToList();
                if (engineTypes.Count == 1)
                {
                    list.Clear();
                    list.Add(engineTypes[0]);
                }
            }
        }
    }
}
