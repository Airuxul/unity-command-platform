using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// The Editor's own selection. Useful as a handoff channel: a caller can read what the user
    /// highlighted, or highlight something so the user can see the result of a command.
    /// </summary>
    public static class SelectionService
    {
        public static Dictionary<string, object> Get()
        {
            var objects = Selection.objects ?? Array.Empty<Object>();

            var assets = new List<object>();
            var nodes = new List<object>();

            foreach (var obj in objects.Where(o => o != null))
            {
                var assetPath = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    assets.Add(new Dictionary<string, object>
                    {
                        ["path"] = assetPath,
                        ["guid"] = AssetDatabase.AssetPathToGUID(assetPath),
                        ["type"] = obj.GetType().Name,
                        ["name"] = obj.name,
                    });
                    continue;
                }

                var go = obj as GameObject ?? (obj as Component)?.gameObject;
                if (go == null)
                    continue;

                nodes.Add(new Dictionary<string, object>
                {
                    ["node"] = GameObjectPath.Of(go.transform),
                    ["name"] = go.name,
                    ["instance_id"] = go.GetInstanceID(),
                    ["scene"] = go.scene.IsValid()
                        ? (string.IsNullOrEmpty(go.scene.path) ? go.scene.name : go.scene.path)
                        : null,
                });
            }

            return new Dictionary<string, object>
            {
                ["count"] = objects.Length,
                ["active"] = SerializedValueReader.Describe(Selection.activeObject),
                ["assets"] = assets,
                ["nodes"] = nodes,
            };
        }

        public static Dictionary<string, object> Set(SelectionSetParams p)
        {
            var resolved = new List<Object>();
            var misses = new List<string>();

            foreach (var assetPath in p.Assets ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                var normalized = AssetPaths.RequireProjectPath(assetPath, "assets");
                var asset = AssetDatabase.LoadMainAssetAtPath(normalized);
                if (asset == null)
                    misses.Add(normalized);
                else
                    resolved.Add(asset);
            }

            var nodePaths = p.Node ?? Array.Empty<string>();
            if (nodePaths.Length > 0)
            {
                using var scope = EditTargetScope.Open(null, p.Scene);
                foreach (var nodePath in nodePaths.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    var node = GameObjectPath.Resolve(scope, nodePath);
                    if (node == null)
                        misses.Add(nodePath);
                    else
                        resolved.Add(node.gameObject);
                }
            }

            foreach (var id in p.Ids ?? Array.Empty<int>())
            {
                var obj = EditorUtility.InstanceIDToObject(id);
                if (obj == null)
                    misses.Add(id.ToString());
                else
                    resolved.Add(obj);
            }

            if (misses.Count > 0)
                throw new ArgumentException($"Could not resolve: {string.Join(", ", misses)}.");

            if (resolved.Count == 0)
                throw new ArgumentException("Pass at least one of 'assets', 'node' or 'ids'.");

            Selection.objects = p.Additive
                ? (Selection.objects ?? Array.Empty<Object>()).Concat(resolved).Distinct().ToArray()
                : resolved.ToArray();

            var result = Get();
            result["additive"] = p.Additive;
            return result;
        }

        public static Dictionary<string, object> Clear()
        {
            var previous = (Selection.objects ?? Array.Empty<Object>()).Length;
            Selection.objects = Array.Empty<Object>();

            return new Dictionary<string, object>
            {
                ["cleared"] = previous,
                ["count"] = 0,
            };
        }
    }
}
