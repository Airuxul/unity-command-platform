using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Hierarchy addressing for node arguments: <c>"Root/Body/Arm"</c>, with <c>"Body[1]"</c> to pick
    /// among same-named siblings. Generated paths only carry an index where one is actually needed,
    /// so the common case stays readable and round-trips.
    /// </summary>
    public static class GameObjectPath
    {
        /// <summary>Resolves a node path, or null when nothing matches.</summary>
        public static Transform Resolve(EditTargetScope scope, string path) => Resolve(RootsOf(scope), path);

        /// <summary>Resolves a node path among <paramref name="roots"/>, or null when nothing matches.</summary>
        public static Transform Resolve(IEnumerable<Transform> roots, string path)
        {
            var segments = Split(path);
            if (segments.Length == 0)
                return null;

            var current = MatchAmong(roots, segments[0]);
            for (var i = 1; i < segments.Length && current != null; i++)
                current = MatchAmong(Children(current), segments[i]);

            return current;
        }

        /// <summary>Resolves a node path or throws with the candidates that would have matched.</summary>
        public static Transform Require(EditTargetScope scope, string path, string paramName = ParamKeys.Node)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                // A prefab has exactly one root, so an omitted node unambiguously means "the root".
                if (scope.IsPrefabContent && scope.PrefabRoot != null)
                    return scope.PrefabRoot.transform;

                throw new ArgumentException($"Parameter '{paramName}' is required for scene targets.");
            }

            return Resolve(scope, path) ?? throw NotFound(RootsOf(scope), path);
        }

        /// <summary>The node_not_found error for <paramref name="path"/>, naming what was there instead.</summary>
        public static UcpException NotFound(IReadOnlyList<Transform> roots, string path)
        {
            var (where, hint) = DescribeNearestMiss(roots, path);
            return new UcpException(UcpErrorCodes.NodeNotFound, $"Node '{path}' not found. {where}", hint);
        }

        static IReadOnlyList<Transform> RootsOf(EditTargetScope scope) =>
            scope.Roots().Where(go => go != null).Select(go => go.transform).ToList();

        /// <summary>Resolves the <c>node</c> path, or the <c>id</c> instance id when given instead.</summary>
        public static Transform Require(EditTargetScope scope, NodeTargetParams p)
        {
            if (p?.Id == null)
                return Require(scope, p?.Node);

            var byId = RequireById(p.Id.Value);
            if (scope.Kind == EditTargetKind.PrefabAsset && byId.scene != scope.Scene)
            {
                throw new ArgumentException(
                    $"Instance ids address objects already in memory. To address a node inside a prefab asset, use '{ParamKeys.Node}'.");
            }

            return byId.transform;
        }

        /// <summary>
        /// Resolves a parent argument. An empty path means "top level" (scene root / prefab root)
        /// and yields null rather than an error.
        /// </summary>
        public static Transform ResolveParent(EditTargetScope scope, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return scope.IsPrefabContent && scope.PrefabRoot != null ? scope.PrefabRoot.transform : null;

            return Require(scope, path, ParamKeys.Parent);
        }

        /// <summary>Canonical path of <paramref name="node"/> within its scene / prefab contents.</summary>
        public static string Of(Transform node)
        {
            if (node == null)
                return null;

            var segments = new List<string>();
            for (var current = node; current != null; current = current.parent)
                segments.Add(Segment(current));

            segments.Reverse();
            return string.Join("/", segments);
        }

        public static string Of(GameObject node) => node == null ? null : Of(node.transform);

        /// <summary>Every transform under the target, roots included, depth-first.</summary>
        public static IEnumerable<Transform> All(EditTargetScope scope)
        {
            foreach (var root in scope.Roots())
            {
                if (root == null)
                    continue;
                foreach (var node in Descend(root.transform))
                    yield return node;
            }
        }

        public static IEnumerable<Transform> Descend(Transform node)
        {
            if (node == null)
                yield break;

            yield return node;
            for (var i = 0; i < node.childCount; i++)
            {
                foreach (var child in Descend(node.GetChild(i)))
                    yield return child;
            }
        }

        /// <summary>Resolves an <c>id</c> argument (Unity instance id) to a GameObject.</summary>
        public static GameObject RequireById(int instanceId)
        {
            var obj = EditorUtility.InstanceIDToObject(instanceId);
            switch (obj)
            {
                case GameObject go:
                    return go;
                case Component component:
                    return component.gameObject;
                case null:
                    throw new UcpException(UcpErrorCodes.NodeNotFound, $"No object with instance id {instanceId}.");
                default:
                    throw new ArgumentException(
                        $"Instance id {instanceId} is a {obj.GetType().Name}, not a GameObject.");
            }
        }

        static IEnumerable<Transform> Children(Transform parent)
        {
            for (var i = 0; i < parent.childCount; i++)
                yield return parent.GetChild(i);
        }

        static Transform MatchAmong(IEnumerable<Transform> candidates, (string name, int index) segment)
        {
            var matchIndex = 0;
            foreach (var candidate in candidates)
            {
                if (candidate == null || !string.Equals(candidate.name, segment.name, StringComparison.Ordinal))
                    continue;
                if (matchIndex == segment.index)
                    return candidate;
                matchIndex++;
            }

            return null;
        }

        static (string name, int index)[] Split(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Array.Empty<(string, int)>();

            return path
                .Replace('\\', '/')
                .Split('/')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(ParseSegment)
                .ToArray();
        }

        static (string name, int index) ParseSegment(string segment)
        {
            var trimmed = segment.Trim();
            if (!trimmed.EndsWith("]", StringComparison.Ordinal))
                return (trimmed, 0);

            var open = trimmed.LastIndexOf('[');
            if (open <= 0)
                return (trimmed, 0);

            var inner = trimmed.Substring(open + 1, trimmed.Length - open - 2);
            return int.TryParse(inner, out var index)
                ? (trimmed.Substring(0, open), index)
                : (trimmed, 0);
        }

        static string Segment(Transform node)
        {
            var siblings = node.parent != null
                ? Children(node.parent)
                : RootSiblings(node);

            var sameName = 0;
            var ownIndex = 0;
            foreach (var sibling in siblings)
            {
                if (sibling == null || !string.Equals(sibling.name, node.name, StringComparison.Ordinal))
                    continue;
                if (sibling == node)
                    ownIndex = sameName;
                sameName++;
            }

            return sameName > 1 ? $"{node.name}[{ownIndex}]" : node.name;
        }

        static IEnumerable<Transform> RootSiblings(Transform node)
        {
            var scene = node.gameObject.scene;
            if (!scene.IsValid())
                return new[] { node };

            return scene.GetRootGameObjects().Select(go => go.transform);
        }

        const int SuggestionCount = 3;

        /// <summary>
        /// Walks as far down <paramref name="path"/> as it can and reports what was available at the
        /// point it gave up, so a caller can correct the path without a separate query. The hint
        /// names the siblings closest to the missing segment.
        /// </summary>
        static (string where, string hint) DescribeNearestMiss(IReadOnlyList<Transform> roots, string path)
        {
            var segments = Split(path);
            if (segments.Length == 0)
                return ("Path is empty.", null);

            var current = MatchAmong(roots, segments[0]);
            if (current == null)
            {
                var names = roots.Where(t => t != null).Select(t => t.name).ToArray();
                return ($"Available roots: {Join(names)}.", Closest(segments[0].name, names));
            }

            for (var i = 1; i < segments.Length; i++)
            {
                var next = MatchAmong(Children(current), segments[i]);
                if (next == null)
                {
                    var names = Children(current).Where(c => c != null).Select(c => c.name).ToArray();
                    return ($"'{Of(current)}' has no child '{segments[i].name}'. Children: {Join(names)}.",
                        Closest(segments[i].name, names, Of(current) + "/"));
                }

                current = next;
            }

            return (string.Empty, null);
        }

        static string Closest(string wanted, string[] names, string prefix = "")
        {
            var closest = names
                .Distinct()
                .OrderBy(n => EditDistance(wanted.ToLowerInvariant(), n.ToLowerInvariant()))
                .ThenBy(n => n, StringComparer.Ordinal)
                .Take(SuggestionCount)
                .Select(n => $"'{prefix}{n}'")
                .ToArray();
            return closest.Length == 0 ? null : $"Did you mean {string.Join(", ", closest)}?";
        }

        static int EditDistance(string a, string b)
        {
            var row = Enumerable.Range(0, b.Length + 1).ToArray();
            for (var i = 1; i <= a.Length; i++)
            {
                var diagonal = row[0];
                row[0] = i;
                for (var j = 1; j <= b.Length; j++)
                {
                    var above = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), diagonal + (a[i - 1] == b[j - 1] ? 0 : 1));
                    diagonal = above;
                }
            }

            return row[b.Length];
        }

        static string Join(string[] names) =>
            names.Length == 0 ? "(none)" : string.Join(", ", names.Distinct().Take(30));
    }
}
