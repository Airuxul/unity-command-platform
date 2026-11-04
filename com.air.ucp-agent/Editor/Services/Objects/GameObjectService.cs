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
    /// Every GameObject operation, written once against <see cref="EditTargetScope"/> so the same
    /// code serves a scene and the inside of a prefab asset. Adding an operation here makes it
    /// available in both places by construction.
    /// </summary>
    public static class GameObjectService
    {
        const int DefaultTreeDepth = 3;
        const int DefaultFindLimit = 200;

        public static Dictionary<string, object> Create(GameObjectCreateParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new ArgumentException("Parameter 'name' is required.");

            using var scope = NodeTargets.Open(p);
            var parent = GameObjectPath.ResolveParent(scope, p.Parent);

            var created = Primitives.Create(p.Primitive);
            created.name = p.Name.Trim();

            if (parent != null)
                created.transform.SetParent(parent, worldPositionStays: false);
            else if (!scope.IsPrefabContent)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(created, scope.Scene);

            // Never hold a Transform across AddComponent: a component that requires a RectTransform
            // (any UI component) makes Unity swap the Transform instance, and the old reference is
            // then a destroyed object. Read created.transform fresh wherever it is needed.
            SetLocalTransform(created.transform, p.Position, p.Rotation, p.Scale);

            if (!string.IsNullOrWhiteSpace(p.Tag))
                created.tag = RequireTag(p.Tag);
            if (!string.IsNullOrWhiteSpace(p.Layer))
                created.layer = NodeTargets.RequireLayer(p.Layer);
            if (p.Active.HasValue)
                created.SetActive(p.Active.Value);

            var added = new List<string>();
            foreach (var typeName in p.Components ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(typeName))
                    continue;

                var type = ComponentTypeResolver.RequireComponent(typeName);
                if (created.GetComponent(type) == null && created.AddComponent(type) == null)
                    throw new InvalidOperationException($"Could not add {type.Name} to '{created.name}'.");

                added.Add(type.Name);
            }

            NodeTargets.RegisterCreated(scope, created, "ucp gameobject_create");
            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(created.transform),
                ["name"] = created.name,
                ["primitive"] = string.IsNullOrWhiteSpace(p.Primitive) ? "empty" : p.Primitive.ToLowerInvariant(),
                ["parent"] = parent == null ? null : GameObjectPath.Of(parent),
                ["components"] = NodeTargets.ComponentNames(created),
                ["components_added"] = added,
                ["instance_id"] = created.GetInstanceID(),
                ["rect_transform"] = created.transform is RectTransform,
            });
        }

        public static Dictionary<string, object> Delete(NodeParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);

            if (scope.IsPrefabContent && node.parent == null)
            {
                throw new InvalidOperationException(
                    "Cannot delete a prefab's root node. Delete the asset with prefab_delete instead.");
            }

            var path = GameObjectPath.Of(node);
            var name = node.name;
            NodeTargets.Destroy(scope, node.gameObject);
            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["node"] = path,
                ["name"] = name,
                ["deleted"] = true,
            });
        }

        public static Dictionary<string, object> Duplicate(GameObjectDuplicateParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var parent = string.IsNullOrWhiteSpace(p.Parent)
                ? node.parent
                : GameObjectPath.Require(scope, p.Parent, "parent");

            if (scope.IsPrefabContent && node.parent == null)
                throw new InvalidOperationException("Cannot duplicate a prefab's root node inside the asset.");

            var clone = Object.Instantiate(node.gameObject, parent);
            clone.name = string.IsNullOrWhiteSpace(p.Name) ? node.name + " (1)" : p.Name.Trim();
            if (parent == null && !scope.IsPrefabContent)
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(clone, scope.Scene);

            NodeTargets.RegisterCreated(scope, clone, "ucp gameobject_duplicate");
            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["source"] = GameObjectPath.Of(node),
                ["node"] = GameObjectPath.Of(clone.transform),
                ["name"] = clone.name,
                ["instance_id"] = clone.GetInstanceID(),
                // Object.Instantiate copies overridden values; whether the copy stays linked to the
                // source prefab is Unity's call, so report it rather than promising either way.
                ["prefab_instance"] = PrefabUtility.IsPartOfPrefabInstance(clone),
            });
        }

        public static Dictionary<string, object> Rename(GameObjectRenameParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var previous = node.name;

            NodeTargets.RecordUndo(scope, node.gameObject, "ucp gameobject_rename");
            node.name = p.Name.Trim();
            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["previous_name"] = previous,
                ["name"] = node.name,
            });
        }

        public static Dictionary<string, object> Reparent(GameObjectReparentParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);

            if (scope.IsPrefabContent && node.parent == null)
                throw new InvalidOperationException("Cannot reparent a prefab's root node.");

            var parent = string.IsNullOrWhiteSpace(p.Parent)
                ? (scope.IsPrefabContent ? scope.PrefabRoot.transform : null)
                : GameObjectPath.Require(scope, p.Parent, "parent");

            if (parent != null && (parent == node || parent.IsChildOf(node)))
                throw new InvalidOperationException($"'{p.Parent}' is inside '{GameObjectPath.Of(node)}'.");

            var keepWorld = p.KeepWorld ?? true;
            var previous = GameObjectPath.Of(node);
            var localPosition = node.localPosition;
            var localRotation = node.localRotation;
            var localScale = node.localScale;

            if (scope.SupportsUndo)
            {
                // Undo.SetTransformParent always keeps the world transform, so restore the local
                // values afterwards rather than silently ignoring keepWorld=false.
                Undo.SetTransformParent(node, parent, "ucp gameobject_reparent");
                if (!keepWorld)
                {
                    node.localPosition = localPosition;
                    node.localRotation = localRotation;
                    node.localScale = localScale;
                }
            }
            else
            {
                node.SetParent(parent, keepWorld);
            }

            if (p.Index.HasValue)
                node.SetSiblingIndex(Mathf.Max(0, p.Index.Value));

            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["previous_node"] = previous,
                ["node"] = GameObjectPath.Of(node),
                ["parent"] = parent == null ? null : GameObjectPath.Of(parent),
                ["sibling_index"] = node.GetSiblingIndex(),
                ["kept_world_transform"] = keepWorld,
            });
        }

        public static Dictionary<string, object> SetTransform(GameObjectTransformParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var world = string.Equals(p.Space, "world", StringComparison.OrdinalIgnoreCase);

            NodeTargets.RecordUndo(scope, node, "ucp gameobject_transform");

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

            if (changed.Count == 0)
                throw new ArgumentException("Pass at least one of 'position', 'rotation' or 'scale'.");

            scope.MarkDirty();

            var payload = Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["changed"] = changed,
                ["space"] = world ? "world" : "local",
                ["relative"] = p.Relative,
            });
            payload["transform"] = DescribeTransform(node);
            return payload;
        }

        public static Dictionary<string, object> Activate(GameObjectActivateParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var target = p.Active ?? !node.gameObject.activeSelf;

            NodeTargets.RecordUndo(scope, node.gameObject, "ucp gameobject_activate");
            node.gameObject.SetActive(target);
            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["active"] = node.gameObject.activeSelf,
                ["active_in_hierarchy"] = node.gameObject.activeInHierarchy,
            });
        }

        public static Dictionary<string, object> SetTag(GameObjectTagParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var tag = RequireTag(p.Tag);

            var touched = Targets(node, p.Recursive).ToList();
            foreach (var target in touched)
            {
                NodeTargets.RecordUndo(scope, target.gameObject, "ucp gameobject_tag");
                target.gameObject.tag = tag;
            }

            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["tag"] = tag,
                ["nodes_changed"] = touched.Count,
            });
        }

        public static Dictionary<string, object> SetLayer(GameObjectLayerParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var layer = NodeTargets.RequireLayer(p.Layer);

            var touched = Targets(node, p.Recursive).ToList();
            foreach (var target in touched)
            {
                NodeTargets.RecordUndo(scope, target.gameObject, "ucp gameobject_layer");
                target.gameObject.layer = layer;
            }

            scope.MarkDirty();

            return Payload(scope, new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["layer"] = layer,
                ["layer_name"] = LayerMask.LayerToName(layer),
                ["nodes_changed"] = touched.Count,
            });
        }

        public static Dictionary<string, object> Info(NodeParams p)
        {
            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var go = node.gameObject;

            var components = new List<object>();
            var all = go.GetComponents<Component>();
            for (var i = 0; i < all.Length; i++)
            {
                var component = all[i];
                if (component == null)
                {
                    components.Add(new Dictionary<string, object>
                    {
                        ["index"] = i,
                        ["type"] = "<missing script>",
                    });
                    continue;
                }

                var entry = new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["type"] = component.GetType().Name,
                    ["full_type"] = component.GetType().FullName,
                };

                if (component is Behaviour behaviour)
                    entry["enabled"] = behaviour.enabled;

                components.Add(entry);
            }

            var payload = Payload(scope, new Dictionary<string, object>
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
                ["components"] = components,
                ["transform"] = DescribeTransform(node),
                ["prefab"] = NodeTargets.DescribePrefabLink(go),
            });

            return payload;
        }

        public static Dictionary<string, object> Tree(GameObjectTreeParams p)
        {
            using var scope = NodeTargets.Open(p);
            var depth = p.Depth ?? DefaultTreeDepth;
            var withComponents = p.Components ?? true;
            var includeInactive = p.IncludeInactive ?? true;

            var roots = new List<object>();
            var count = 0;

            if (!string.IsNullOrWhiteSpace(p.Node) || p.Id != null || scope.IsPrefabContent)
            {
                var node = NodeTargets.Require(scope, p);
                roots.Add(Describe(node, depth, withComponents, includeInactive, ref count));
            }
            else
            {
                foreach (var root in scope.Roots())
                {
                    if (root == null || (!includeInactive && !root.activeSelf))
                        continue;

                    roots.Add(Describe(root.transform, depth, withComponents, includeInactive, ref count));
                }
            }

            return Payload(scope, new Dictionary<string, object>
            {
                ["depth"] = depth,
                ["node_count"] = count,
                ["roots"] = roots,
            });
        }

        public static Dictionary<string, object> Find(GameObjectFindParams p)
        {
            using var scope = NodeTargets.Open(p);
            var limit = p.Limit ?? DefaultFindLimit;
            var includeInactive = p.IncludeInactive ?? true;
            var componentType = string.IsNullOrWhiteSpace(p.Component)
                ? null
                : ComponentTypeResolver.RequireComponent(p.Component);
            var layer = string.IsNullOrWhiteSpace(p.Layer) ? (int?)null : NodeTargets.RequireLayer(p.Layer);

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
                    ["components"] = NodeTargets.ComponentNames(go),
                });
            }

            return Payload(scope, new Dictionary<string, object>
            {
                ["count"] = matches.Count,
                ["total_matches"] = total,
                ["truncated"] = total > matches.Count,
                ["limit"] = limit,
                ["matches"] = matches,
            });
        }

        // --- shared shaping ----------------------------------------------------------------

        /// <summary>Every payload carries the resolved target so the caller is never guessing.</summary>
        internal static Dictionary<string, object> Payload(
            EditTargetScope scope,
            Dictionary<string, object> body)
        {
            body["target"] = scope.Describe();
            return body;
        }

        internal static Dictionary<string, object> DescribeTransform(Transform node) =>
            new Dictionary<string, object>
            {
                ["local_position"] = ToArray(node.localPosition),
                ["local_euler"] = ToArray(node.localEulerAngles),
                ["local_scale"] = ToArray(node.localScale),
                ["position"] = ToArray(node.position),
                ["euler"] = ToArray(node.eulerAngles),
                ["lossy_scale"] = ToArray(node.lossyScale),
            };

        internal static float[] ToArray(Vector3 value) => new[] { value.x, value.y, value.z };

        static void SetLocalTransform(
            Transform node,
            Newtonsoft.Json.Linq.JToken position,
            Newtonsoft.Json.Linq.JToken rotation,
            Newtonsoft.Json.Linq.JToken scale)
        {
            node.localPosition = JsonValues.IsAbsent(position)
                ? Vector3.zero
                : JsonValues.ToVector3(position, "position");
            node.localRotation = JsonValues.IsAbsent(rotation)
                ? Quaternion.identity
                : JsonValues.ToQuaternion(rotation, "rotation");
            node.localScale = JsonValues.IsAbsent(scale)
                ? Vector3.one
                : JsonValues.ToVector3(scale, "scale");
        }

        static Dictionary<string, object> Describe(
            Transform node,
            int depth,
            bool withComponents,
            bool includeInactive,
            ref int count)
        {
            count++;

            var entry = new Dictionary<string, object>
            {
                ["name"] = node.name,
                ["node"] = GameObjectPath.Of(node),
                ["active"] = node.gameObject.activeSelf,
                ["child_count"] = node.childCount,
            };

            if (withComponents)
                entry["components"] = NodeTargets.ComponentNames(node.gameObject);

            // Nested prefabs read as instance roots even inside prefab contents, so this marks the
            // boundary where edits become overrides instead of direct changes.
            if (PrefabUtility.IsAnyPrefabInstanceRoot(node.gameObject))
            {
                var source = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject);
                if (!string.IsNullOrEmpty(source))
                    entry["nested_prefab"] = source;
            }

            if (depth == 0 || node.childCount == 0)
                return entry;

            var children = new List<object>();
            for (var i = 0; i < node.childCount; i++)
            {
                var child = node.GetChild(i);
                if (!includeInactive && !child.gameObject.activeSelf)
                    continue;

                children.Add(Describe(child, depth < 0 ? -1 : depth - 1, withComponents, includeInactive, ref count));
            }

            entry["children"] = children;
            return entry;
        }

        static IEnumerable<Transform> Targets(Transform node, bool recursive) =>
            recursive ? GameObjectPath.Descend(node) : new[] { node };

        static string RequireTag(string tag)
        {
            var trimmed = tag?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new ArgumentException("Parameter 'tag' is required.");

            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            if (Array.IndexOf(tags, trimmed) < 0)
            {
                throw new ArgumentException(
                    $"Unknown tag '{trimmed}'. Defined tags: {string.Join(", ", tags)}. Add one with tag_add.");
            }

            return trimmed;
        }
    }
}
