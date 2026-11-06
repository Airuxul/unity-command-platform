using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// Every GameObject operation, written once against <see cref="EditTargetScope"/> so the same
    /// code serves a scene and the inside of a prefab asset. Adding an operation here makes it
    /// available in both places by construction.
    /// </summary>
    public static class GameObjectService
    {
        const int DefaultTreeDepth = 3;
        const int DefaultFindLimit = 200;

        public static Dictionary<string, object> Create(EditTargetScope scope, GameObjectCreateParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new ArgumentException("Parameter 'name' is required.");

            var parent = GameObjectPath.ResolveParent(scope, p.Parent);

            var created = Primitives.Create(p.Primitive);
            created.name = p.Name.Trim();

            if (parent != null)
                created.transform.SetParent(parent, worldPositionStays: false);
            else if (!scope.IsPrefabContent)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, scope.Scene);

            // Registered before anything below can throw, so a failed create is undone with the rest.
            scope.RegisterCreated(created, $"ucp {CommandNames.GameObjectCreate}");

            // Never hold a Transform across AddComponent: a component that requires a RectTransform
            // (any UI component) makes Unity swap the Transform instance, and the old reference is
            // then a destroyed object. Read created.transform fresh wherever it is needed.
            // A new node already sits at local zero / identity / one under its parent.
            TransformValues.ApplyLocal(created.transform, p.Position, p.Rotation, p.Scale);

            if (!string.IsNullOrWhiteSpace(p.Tag))
                created.tag = TagsAndLayers.RequireTag(p.Tag);
            if (!string.IsNullOrWhiteSpace(p.Layer))
                created.layer = TagsAndLayers.RequireLayer(p.Layer);
            if (p.Active.HasValue)
                created.SetActive(p.Active.Value);

            var added = Primitives.AddComponents(created, p.Components);

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(created.transform),
                ["name"] = created.name,
                ["primitive"] = Primitives.Name(p.Primitive),
                ["parent"] = parent == null ? null : GameObjectPath.Of(parent),
                ["components"] = ComponentService.Names(created),
                ["components_added"] = added,
                ["instance_id"] = created.GetInstanceID(),
                ["rect_transform"] = created.transform is RectTransform,
            };
        }

        public static Dictionary<string, object> Delete(EditTargetScope scope, NodeParams p)
        {
            var node = GameObjectPath.Require(scope, p);

            if (scope.IsPrefabContent && node.parent == null)
            {
                throw new InvalidOperationException(
                    $"Cannot delete a prefab's root node. Delete the asset with {CommandNames.PrefabDelete} instead.");
            }

            var path = GameObjectPath.Of(node);
            var name = node.name;
            scope.Destroy(node.gameObject);

            return new Dictionary<string, object>
            {
                ["node"] = path,
                ["name"] = name,
                ["deleted"] = true,
            };
        }

        public static Dictionary<string, object> Duplicate(EditTargetScope scope, GameObjectDuplicateParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var parent = string.IsNullOrWhiteSpace(p.Parent)
                ? node.parent
                : GameObjectPath.Require(scope, p.Parent, ParamKeys.Parent);

            if (scope.IsPrefabContent && node.parent == null)
                throw new InvalidOperationException("Cannot duplicate a prefab's root node inside the asset.");

            var clone = Object.Instantiate(node.gameObject, parent);
            clone.name = string.IsNullOrWhiteSpace(p.Name) ? node.name + " (1)" : p.Name.Trim();
            if (parent == null && !scope.IsPrefabContent)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scope.Scene);

            scope.RegisterCreated(clone, $"ucp {CommandNames.GameObjectDuplicate}");

            return new Dictionary<string, object>
            {
                ["source"] = GameObjectPath.Of(node),
                ["node"] = GameObjectPath.Of(clone.transform),
                ["name"] = clone.name,
                ["instance_id"] = clone.GetInstanceID(),
                // Object.Instantiate copies overridden values; whether the copy stays linked to the
                // source prefab is Unity's call, so report it rather than promising either way.
                ["prefab_instance"] = PrefabUtility.IsPartOfPrefabInstance(clone),
            };
        }

        public static Dictionary<string, object> Rename(EditTargetScope scope, GameObjectRenameParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var previous = node.name;

            scope.RecordUndo(node.gameObject, $"ucp {CommandNames.GameObjectRename}");
            node.name = p.Name.Trim();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["previous_name"] = previous,
                ["name"] = node.name,
            };
        }

        public static Dictionary<string, object> Reparent(EditTargetScope scope, GameObjectReparentParams p)
        {
            var node = GameObjectPath.Require(scope, p);

            if (scope.IsPrefabContent && node.parent == null)
                throw new InvalidOperationException("Cannot reparent a prefab's root node.");

            var parent = string.IsNullOrWhiteSpace(p.Parent)
                ? (scope.IsPrefabContent ? scope.PrefabRoot.transform : null)
                : GameObjectPath.Require(scope, p.Parent, ParamKeys.Parent);

            if (parent != null && (parent == node || parent.IsChildOf(node)))
                throw new InvalidOperationException($"'{p.Parent}' is inside '{GameObjectPath.Of(node)}'.");

            var keepWorld = p.KeepWorld ?? true;
            var previous = GameObjectPath.Of(node);
            scope.SetParent(node, parent, keepWorld, $"ucp {CommandNames.GameObjectReparent}");

            if (p.Index.HasValue)
                node.SetSiblingIndex(Mathf.Max(0, p.Index.Value));

            return new Dictionary<string, object>
            {
                ["previous_node"] = previous,
                ["node"] = GameObjectPath.Of(node),
                ["parent"] = parent == null ? null : GameObjectPath.Of(parent),
                ["sibling_index"] = node.GetSiblingIndex(),
                ["kept_world_transform"] = keepWorld,
            };
        }

        public static Dictionary<string, object> SetTransform(EditTargetScope scope, GameObjectTransformParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var world = string.Equals(p.Space, "world", StringComparison.OrdinalIgnoreCase);
            if (JsonValues.IsAbsent(p.Position) && JsonValues.IsAbsent(p.Rotation) && JsonValues.IsAbsent(p.Scale))
                throw new ArgumentException("Pass at least one of 'position', 'rotation' or 'scale'.");

            scope.RecordUndo(node, $"ucp {CommandNames.GameObjectTransform}");

            var changed = new List<string>();

            if (!JsonValues.IsAbsent(p.Position))
            {
                var value = JsonValues.ToVector3(p.Position, "position");
                if (world)
                    node.position = p.Relative ? node.position + value : value;
                else
                    node.localPosition = p.Relative ? node.localPosition + value : value;

                changed.Add("position");
            }

            if (!JsonValues.IsAbsent(p.Rotation))
            {
                var value = JsonValues.ToQuaternion(p.Rotation, "rotation");
                if (world)
                    node.rotation = p.Relative ? node.rotation * value : value;
                else
                    node.localRotation = p.Relative ? node.localRotation * value : value;

                changed.Add("rotation");
            }

            if (!JsonValues.IsAbsent(p.Scale))
            {
                var value = JsonValues.ToVector3(p.Scale, "scale");
                node.localScale = p.Relative
                    ? Vector3.Scale(node.localScale, value)
                    : value;

                changed.Add("scale");
            }

            var payload = new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["changed"] = changed,
                ["space"] = world ? "world" : "local",
                ["relative"] = p.Relative,
            };
            payload["transform"] = TransformValues.Describe(node);
            return payload;
        }

        public static Dictionary<string, object> Activate(EditTargetScope scope, GameObjectActivateParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var target = p.Active ?? !node.gameObject.activeSelf;

            scope.RecordUndo(node.gameObject, $"ucp {CommandNames.GameObjectActivate}");
            node.gameObject.SetActive(target);

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["active"] = node.gameObject.activeSelf,
                ["active_in_hierarchy"] = node.gameObject.activeInHierarchy,
            };
        }

        public static Dictionary<string, object> SetTag(EditTargetScope scope, GameObjectTagParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var tag = TagsAndLayers.RequireTag(p.Tag);

            var touched = Targets(node, p.Recursive).ToList();
            foreach (var target in touched)
            {
                scope.RecordUndo(target.gameObject, $"ucp {CommandNames.GameObjectTag}");
                target.gameObject.tag = tag;
            }

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["tag"] = tag,
                ["nodes_changed"] = touched.Count,
            };
        }

        public static Dictionary<string, object> SetLayer(EditTargetScope scope, GameObjectLayerParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var layer = TagsAndLayers.RequireLayer(p.Layer);

            var touched = Targets(node, p.Recursive).ToList();
            foreach (var target in touched)
            {
                scope.RecordUndo(target.gameObject, $"ucp {CommandNames.GameObjectLayer}");
                target.gameObject.layer = layer;
            }

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["layer"] = layer,
                ["layer_name"] = LayerMask.LayerToName(layer),
                ["nodes_changed"] = touched.Count,
            };
        }

        public static Dictionary<string, object> Info(EditTargetScope scope, NodeParams p)
        {
            var node = GameObjectPath.Require(scope, p);
            var go = node.gameObject;

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["name"] = go.name,
                ["instance_id"] = go.GetInstanceID(),
                ["active"] = go.activeSelf,
                ["active_in_hierarchy"] = go.activeInHierarchy,
                ["tag"] = go.tag,
                ["layer"] = go.layer,
                ["layer_name"] = LayerMask.LayerToName(go.layer),
                ["static_flags"] = GameObjectUtility.GetStaticEditorFlags(go).ToString(),
                ["parent"] = node.parent == null ? null : GameObjectPath.Of(node.parent),
                ["sibling_index"] = node.GetSiblingIndex(),
                ["child_count"] = node.childCount,
                ["children"] = Enumerable.Range(0, node.childCount).Select(i => node.GetChild(i).name).ToList(),
                ["components"] = ComponentService.Describe(go),
                ["transform"] = TransformValues.Describe(node),
                ["prefab"] = PrefabInstanceService.Link(go),
            };
        }

        public static Dictionary<string, object> Tree(EditTargetScope scope, GameObjectTreeParams p)
        {
            var depth = p.Depth ?? DefaultTreeDepth;
            var includeInactive = p.IncludeInactive ?? true;
            var tree = new TreeShape(p.Limit, p.Format, p.Fields, p.Components ?? true, includeInactive);

            if (!string.IsNullOrWhiteSpace(p.Node) || p.Id != null || scope.IsPrefabContent)
            {
                tree.Add(GameObjectPath.Require(scope, p), depth);
            }
            else
            {
                foreach (var root in scope.Roots())
                {
                    if (root == null || (!includeInactive && !root.activeSelf))
                        continue;

                    tree.Add(root.transform, depth);
                }
            }

            return tree.Result(depth);
        }

        public static Dictionary<string, object> Find(EditTargetScope scope, GameObjectFindParams p)
        {
            var limit = p.Limit ?? DefaultFindLimit;
            var includeInactive = p.IncludeInactive ?? true;
            var componentType = string.IsNullOrWhiteSpace(p.Component)
                ? null
                : ComponentTypeResolver.RequireComponent(p.Component);
            var layer = string.IsNullOrWhiteSpace(p.Layer) ? (int?)null : TagsAndLayers.RequireLayer(p.Layer);

            if (string.IsNullOrWhiteSpace(p.Name) && string.IsNullOrWhiteSpace(p.Tag) &&
                layer == null && componentType == null && string.IsNullOrWhiteSpace(p.Path))
            {
                throw new ArgumentException(
                    "Pass at least one filter: name, tag, layer, component or path.");
            }

            var matches = new List<object>();
            var total = 0;

            foreach (var node in GameObjectPath.All(scope))
            {
                var go = node.gameObject;
                if (!includeInactive && !go.activeInHierarchy)
                    continue;

                if (!string.IsNullOrWhiteSpace(p.Name))
                {
                    var hit = p.Exact
                        ? string.Equals(go.name, p.Name, StringComparison.Ordinal)
                        : go.name.IndexOf(p.Name, StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!hit)
                        continue;
                }

                if (!string.IsNullOrWhiteSpace(p.Tag) && !string.Equals(go.tag, p.Tag, StringComparison.Ordinal))
                    continue;

                if (layer != null && go.layer != layer.Value)
                    continue;

                if (componentType != null && go.GetComponent(componentType) == null)
                    continue;

                var path = GameObjectPath.Of(node);
                if (!string.IsNullOrWhiteSpace(p.Path) &&
                    path.IndexOf(p.Path, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                total++;
                if (matches.Count >= limit)
                    continue;

                matches.Add(new Dictionary<string, object>
                {
                    ["node"] = path,
                    ["name"] = go.name,
                    ["instance_id"] = go.GetInstanceID(),
                    ["active"] = go.activeSelf,
                    ["tag"] = go.tag,
                    ["layer_name"] = LayerMask.LayerToName(go.layer),
                    ["components"] = ComponentService.Names(go),
                });
            }

            return new Dictionary<string, object>
            {
                ["count"] = matches.Count,
                ["total_matches"] = total,
                ["truncated"] = total > matches.Count,
                ["limit"] = limit,
                ["matches"] = matches,
            };
        }

        // --- shared shaping ----------------------------------------------------------------

        static IEnumerable<Transform> Targets(Transform node, bool recursive) =>
            recursive ? GameObjectPath.Descend(node) : new[] { node };
    }
}
