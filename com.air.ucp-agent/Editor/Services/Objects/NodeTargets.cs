using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Glue between command parameters and <see cref="EditTargetScope"/>: opening the target,
    /// resolving the addressed node, and the undo/destroy shims that differ between a scene and a
    /// prefab asset loaded off-screen.
    /// </summary>
    public static class NodeTargets
    {
        public static EditTargetScope Open(EditTargetParams p) =>
            EditTargetScope.Open(p?.Prefab, p?.Scene);

        /// <summary>Resolves the <c>node</c> path, or the <c>id</c> instance id when given instead.</summary>
        public static Transform Require(EditTargetScope scope, NodeTargetParams p)
        {
            if (p?.Id != null)
            {
                var byId = GameObjectPath.RequireById(p.Id.Value);
                if (scope.Kind == EditTargetKind.PrefabAsset && byId.scene != scope.Scene)
                {
                    throw new ArgumentException(
                        "Instance ids address objects already in memory. To address a node inside a prefab asset, use 'node'.");
                }

                return byId.transform;
            }

            return GameObjectPath.Require(scope, p?.Node);
        }

        public static void RecordUndo(EditTargetScope scope, Object target, string label)
        {
            if (scope.SupportsUndo && target != null)
                Undo.RecordObject(target, label);
        }

        public static void RegisterCreated(EditTargetScope scope, GameObject created, string label)
        {
            if (scope.SupportsUndo && created != null)
                Undo.RegisterCreatedObjectUndo(created, label);
        }

        public static void Destroy(EditTargetScope scope, GameObject target)
        {
            if (target == null)
                return;

            if (scope.SupportsUndo)
                Undo.DestroyObjectImmediate(target);
            else
                Object.DestroyImmediate(target);
        }

        public static void Destroy(EditTargetScope scope, Component target)
        {
            if (target == null)
                return;

            if (scope.SupportsUndo)
                Undo.DestroyObjectImmediate(target);
            else
                Object.DestroyImmediate(target);
        }

        public static SerializedValueWriter.Context WriterContext(EditTargetScope scope) =>
            new SerializedValueWriter.Context { Scope = scope, RecordUndo = scope.SupportsUndo };

        /// <summary>Component type names on a node, with missing scripts called out rather than dropped.</summary>
        public static List<string> ComponentNames(GameObject node) =>
            node.GetComponents<Component>()
                .Select(c => c == null ? "<missing script>" : c.GetType().Name)
                .ToList();

        /// <summary>
        /// Prefab linkage for a node: the nested-prefab / instance-root information a caller needs to
        /// know whether editing here will be treated as an override.
        /// </summary>
        public static Dictionary<string, object> DescribePrefabLink(GameObject node)
        {
            var isInstanceRoot = PrefabUtility.IsAnyPrefabInstanceRoot(node);
            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node);

            var result = new Dictionary<string, object>
            {
                ["is_prefab_instance_root"] = isInstanceRoot,
                ["is_part_of_prefab_instance"] = PrefabUtility.IsPartOfPrefabInstance(node),
                ["source_prefab"] = string.IsNullOrEmpty(assetPath) ? null : assetPath,
            };

            if (PrefabUtility.IsPartOfPrefabInstance(node))
                result["instance_status"] = PrefabUtility.GetPrefabInstanceStatus(node).ToString();

            return result;
        }

        /// <summary>Resolves a layer given as either a name or an index.</summary>
        public static int RequireLayer(string layer)
        {
            if (string.IsNullOrWhiteSpace(layer))
                throw new ArgumentException("Parameter 'layer' is required.");

            var trimmed = layer.Trim();
            if (int.TryParse(trimmed, out var index))
            {
                if (index < 0 || index > 31)
                    throw new ArgumentException($"Layer index {index} is out of range (0-31).");

                return index;
            }

            var byName = LayerMask.NameToLayer(trimmed);
            if (byName >= 0)
                return byName;

            var defined = Enumerable.Range(0, 32)
                .Select(LayerMask.LayerToName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();
            throw new ArgumentException(
                $"Unknown layer '{layer}'. Defined layers: {string.Join(", ", defined)}. Add one with layer_add.");
        }
    }
}
