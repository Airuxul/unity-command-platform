using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Writes a hierarchy for gameobject_tree / prefab_tree within a node budget, so a large scene
    /// cannot flood the caller: nested JSON, or one line per node.
    /// </summary>
    public sealed class TreeShape
    {
        public const string Json = "json";
        public const string Compact = "compact";
        public const string Formats = Json + "|" + Compact;

        public const string Fields = "name|node|active|child_count|components|nested_prefab";

        public const int DefaultLimit = 500;

        readonly int _limit;
        readonly bool _compact;
        readonly bool _withComponents;
        readonly bool _includeInactive;
        readonly HashSet<string> _fields;

        readonly List<object> _roots = new();
        readonly List<string> _lines = new();
        int _emitted;
        int _total;

        public TreeShape(int? limit, string format, string fields, bool withComponents, bool includeInactive)
        {
            _limit = limit is > 0 ? limit.Value : DefaultLimit;
            _compact = string.Equals(format, Compact, StringComparison.OrdinalIgnoreCase);
            _withComponents = withComponents;
            _includeInactive = includeInactive;
            _fields = string.IsNullOrWhiteSpace(fields)
                ? null
                : new HashSet<string>(
                    fields.Split(',').Select(f => f.Trim().ToLowerInvariant()).Where(f => f.Length > 0));
        }

        /// <summary>Walks <paramref name="root"/> <paramref name="depth"/> levels down; -1 for all.</summary>
        public void Add(Transform root, int depth)
        {
            var entry = Visit(root, depth);
            if (entry != null)
                _roots.Add(entry);
        }

        public Dictionary<string, object> Result(int depth)
        {
            var result = new Dictionary<string, object>
            {
                ["depth"] = depth,
                ["node_count"] = _emitted,
                ["node_count_total"] = _total,
                ["truncated"] = _total > _emitted,
                ["limit"] = _limit,
            };
            result[_compact ? "lines" : "roots"] = _compact ? _lines : _roots;
            return result;
        }

        /// <summary>
        /// Counts every node in range, so the caller learns how much was cut, but only describes
        /// the first <c>limit</c> in depth-first order. Null once the budget is spent.
        /// </summary>
        Dictionary<string, object> Visit(Transform node, int depth)
        {
            _total++;
            var emit = _emitted < _limit;
            Dictionary<string, object> entry = null;
            if (emit)
            {
                _emitted++;
                if (_compact)
                    _lines.Add(Line(node));
                else
                    entry = Describe(node);
            }

            if (depth == 0 || node.childCount == 0)
                return entry;

            var children = new List<object>();
            for (var i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (!_includeInactive && !child.gameObject.activeSelf)
                    continue;

                var described = Visit(child, depth < 0 ? -1 : depth - 1);
                if (described != null)
                    children.Add(described);
            }

            if (entry != null)
                entry["children"] = children;
            return entry;
        }

        Dictionary<string, object> Describe(Transform node)
        {
            var entry = new Dictionary<string, object>();
            Put(entry, "name", node.name);
            Put(entry, "node", GameObjectPath.Of(node));
            Put(entry, "active", node.gameObject.activeSelf);
            Put(entry, "child_count", node.childCount);
            if (_withComponents)
                Put(entry, "components", ComponentService.Names(node.gameObject));

            // Nested prefabs read as instance roots even inside prefab contents, so this marks the
            // boundary where edits become overrides instead of direct changes.
            var source = NestedPrefab(node);
            if (source != null)
                Put(entry, "nested_prefab", source);

            return entry;
        }

        string Line(Transform node)
        {
            var line = GameObjectPath.Of(node);
            if (_withComponents)
                line += $" [{string.Join(",", ComponentService.Names(node.gameObject))}]";
            if (!node.gameObject.activeSelf)
                line += " (inactive)";
            return line;
        }

        void Put(Dictionary<string, object> entry, string field, object value)
        {
            if (_fields == null || _fields.Contains(field))
                entry[field] = value;
        }

        static string NestedPrefab(Transform node)
        {
            if (!PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject))
                return null;

            var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject);
            return string.IsNullOrEmpty(source) ? null : source;
        }
    }
}
