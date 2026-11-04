using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services.Prefab
{
    /// <summary>
    /// Prefab instances: instantiate (into a scene or, for nesting, into another prefab asset),
    /// inspect overrides, apply, revert, unpack, and swap the source asset.
    /// </summary>
    public static class PrefabInstanceService
    {
        public static Dictionary<string, object> Instantiate(EditTargetScope scope, PrefabInstantiateParams p)
        {
            var sourcePath = AssetPaths.RequireExisting(p.Source, "source");
            var sourceAsset = AssetPaths.RequireAsset<GameObject>(sourcePath, "source");

            if (scope.IsPrefabContent &&
                string.Equals(scope.PrefabPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{sourcePath}' cannot be nested inside itself.");
            }

            var parent = GameObjectPath.ResolveParent(scope, p.Parent);

            var instance = PrefabUtility.InstantiatePrefab(sourceAsset, scope.Scene) as GameObject;
            if (instance == null)
                throw new InvalidOperationException($"Could not instantiate '{sourcePath}'.");

            // Registered before anything below can throw, so a failed instantiate is undone with the rest.
            NodeTargets.RegisterCreated(scope, instance, "ucp prefab_instantiate");

            if (parent != null)
                instance.transform.SetParent(parent, worldPositionStays: false);

            if (!string.IsNullOrWhiteSpace(p.Name))
                instance.name = p.Name.Trim();

            var transform = instance.transform;
            if (!JsonValues.IsAbsent(p.Position))
                transform.localPosition = JsonValues.ToVector3(p.Position, "position");
            if (!JsonValues.IsAbsent(p.Rotation))
                transform.localRotation = JsonValues.ToQuaternion(p.Rotation, "rotation");
            if (!JsonValues.IsAbsent(p.Scale))
                transform.localScale = JsonValues.ToVector3(p.Scale, "scale");
            if (p.Index.HasValue)
                transform.SetSiblingIndex(Mathf.Max(0, p.Index.Value));

            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["source"] = sourcePath,
                ["node"] = GameObjectPath.Of(transform),
                ["name"] = instance.name,
                ["instance_id"] = instance.GetInstanceID(),
                ["parent"] = parent == null ? null : GameObjectPath.Of(parent),
                ["nested"] = scope.IsPrefabContent,
                ["transform"] = GameObjectService.DescribeTransform(transform),
                ["target"] = scope.Describe(),
            };
        }

        public static Dictionary<string, object> Apply(EditTargetScope scope, PrefabOverrideParams p)
        {
            var node = NodeTargets.Require(scope, p);
            var (root, assetPath) = RequireInstance(node.gameObject);

            var scopeLabel = "instance";
            if (!string.IsNullOrWhiteSpace(p.Property))
            {
                var property = RequireProperty(node.gameObject, p, out var component);
                PrefabUtility.ApplyPropertyOverride(property, assetPath, InteractionMode.AutomatedAction);
                scopeLabel = $"{component.GetType().Name}.{property.propertyPath}";
            }
            else if (!string.IsNullOrWhiteSpace(p.Component))
            {
                var component = ComponentService.Require(node.gameObject, p.Component, p.Index);
                PrefabUtility.ApplyObjectOverride(component, assetPath, InteractionMode.AutomatedAction);
                scopeLabel = component.GetType().Name;
            }
            else
            {
                PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
            }

            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["instance_root"] = GameObjectPath.Of(root.transform),
                ["applied_to"] = assetPath,
                ["applied_scope"] = scopeLabel,
                ["remaining_overrides"] = CountOverrides(root),
                ["target"] = scope.Describe(),
            };
        }

        public static Dictionary<string, object> Revert(EditTargetScope scope, PrefabOverrideParams p)
        {
            var node = NodeTargets.Require(scope, p);
            var (root, assetPath) = RequireInstance(node.gameObject);

            var scopeLabel = "instance";
            if (!string.IsNullOrWhiteSpace(p.Property))
            {
                var property = RequireProperty(node.gameObject, p, out var component);
                PrefabUtility.RevertPropertyOverride(property, InteractionMode.AutomatedAction);
                scopeLabel = $"{component.GetType().Name}.{property.propertyPath}";
            }
            else if (!string.IsNullOrWhiteSpace(p.Component))
            {
                var component = ComponentService.Require(node.gameObject, p.Component, p.Index);
                PrefabUtility.RevertObjectOverride(component, InteractionMode.AutomatedAction);
                scopeLabel = component.GetType().Name;
            }
            else
            {
                PrefabUtility.RevertPrefabInstance(root, InteractionMode.AutomatedAction);
            }

            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["instance_root"] = GameObjectPath.Of(root.transform),
                ["source"] = assetPath,
                ["reverted_scope"] = scopeLabel,
                ["remaining_overrides"] = CountOverrides(root),
                ["target"] = scope.Describe(),
            };
        }

        public static Dictionary<string, object> Overrides(EditTargetScope scope, PrefabOverridesParams p)
        {
            var node = NodeTargets.Require(scope, p);
            var (root, assetPath) = RequireInstance(node.gameObject);

            var modifications = (PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>())
                .Where(m => m != null)
                .Select(m => new Dictionary<string, object>
                {
                    ["target"] = m.target == null ? null : m.target.GetType().Name,
                    ["target_node"] = NodeOf(m.target),
                    ["property"] = m.propertyPath,
                    ["value"] = m.value,
                    ["object_reference"] = SerializedValueReader.Describe(m.objectReference),
                })
                .Cast<object>()
                .ToList();

            var added = PrefabUtility.GetAddedComponents(root)
                .Where(a => a?.instanceComponent != null)
                .Select(a => new Dictionary<string, object>
                {
                    ["node"] = GameObjectPath.Of(a.instanceComponent.transform),
                    ["component"] = a.instanceComponent.GetType().Name,
                })
                .Cast<object>()
                .ToList();

            var removed = PrefabUtility.GetRemovedComponents(root)
                .Where(r => r?.assetComponent != null)
                .Select(r => new Dictionary<string, object>
                {
                    ["node"] = r.containingInstanceGameObject == null
                        ? null
                        : GameObjectPath.Of(r.containingInstanceGameObject.transform),
                    ["component"] = r.assetComponent.GetType().Name,
                })
                .Cast<object>()
                .ToList();

            var addedObjects = PrefabUtility.GetAddedGameObjects(root)
                .Where(a => a?.instanceGameObject != null)
                .Select(a => new Dictionary<string, object>
                {
                    ["node"] = GameObjectPath.Of(a.instanceGameObject.transform),
                    ["sibling_index"] = a.siblingIndex,
                })
                .Cast<object>()
                .ToList();

            var objectOverrides = PrefabUtility.GetObjectOverrides(root, p.IncludeDefaults)
                .Where(o => o?.instanceObject != null)
                .Select(o => new Dictionary<string, object>
                {
                    ["object"] = o.instanceObject.GetType().Name,
                    ["node"] = NodeOf(o.instanceObject),
                })
                .Cast<object>()
                .ToList();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["instance_root"] = GameObjectPath.Of(root.transform),
                ["source"] = assetPath,
                ["include_defaults"] = p.IncludeDefaults,
                // Unity treats a root's transform and name as "default overrides" that exist on
                // every instance and are never applied. Counting the raw modification list would
                // therefore report overrides on a freshly instantiated, untouched prefab.
                ["has_overrides"] = PrefabUtility.HasPrefabInstanceAnyOverrides(root, p.IncludeDefaults),
                ["property_modifications_include_defaults"] = true,
                ["property_modifications"] = modifications,
                ["modified_objects"] = objectOverrides,
                ["added_components"] = added,
                ["removed_components"] = removed,
                ["added_gameobjects"] = addedObjects,
                ["target"] = scope.Describe(),
            };
        }

        public static Dictionary<string, object> Unpack(EditTargetScope scope, PrefabUnpackParams p)
        {
            var node = NodeTargets.Require(scope, p);
            var (root, assetPath) = RequireInstance(node.gameObject);

            var mode = p.Completely ? PrefabUnpackMode.Completely : PrefabUnpackMode.OutermostRoot;
            PrefabUtility.UnpackPrefabInstance(root, mode, InteractionMode.AutomatedAction);
            scope.MarkDirty();

            // OutermostRoot removes one level of linkage. Unpacking a *variant* instance therefore
            // leaves an instance of the variant's base, and unpacking a prefab that contains nested
            // prefabs leaves those nested instances intact. Pass completely=true for a clean break.
            var stillInstance = PrefabUtility.IsPartOfPrefabInstance(node.gameObject);
            var stillInstanceOf = stillInstance
                ? PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(node.gameObject)
                : null;

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["was_instance_of"] = assetPath,
                ["mode"] = mode.ToString(),
                ["still_instance"] = stillInstance,
                ["still_instance_of"] = string.IsNullOrEmpty(stillInstanceOf) ? null : stillInstanceOf,
                ["target"] = scope.Describe(),
            };
        }

        /// <summary>
        /// Swaps an instance's source prefab by re-instantiating in place. Done by hand rather than
        /// with <c>ReplacePrefabAssetOfPrefabInstance</c> so this works on every supported Unity
        /// version; instance overrides are not carried across.
        /// </summary>
        public static Dictionary<string, object> Replace(EditTargetScope scope, PrefabReplaceParams p)
        {
            var newPath = AssetPaths.RequireExisting(p.Path, "path");
            var newAsset = AssetPaths.RequireAsset<GameObject>(newPath, "path");

            var node = NodeTargets.Require(scope, p);
            var (root, oldPath) = RequireInstance(node.gameObject);

            if (string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"'{GameObjectPath.Of(root.transform)}' is already an instance of '{newPath}'.");
            }

            if (scope.IsPrefabContent && root == scope.PrefabRoot)
            {
                throw new InvalidOperationException(
                    $"'{root.name}' is the root of '{scope.PrefabPath}' itself and cannot be replaced. Create a variant of '{newPath}' instead (prefab_create_variant).");
            }

            var keepTransform = p.KeepTransform ?? true;
            var oldTransform = root.transform;
            var parent = oldTransform.parent;
            var siblingIndex = oldTransform.GetSiblingIndex();
            var localPosition = oldTransform.localPosition;
            var localRotation = oldTransform.localRotation;
            var localScale = oldTransform.localScale;
            var name = root.name;
            var wasActive = root.activeSelf;

            var replacement = PrefabUtility.InstantiatePrefab(newAsset, scope.Scene) as GameObject;
            if (replacement == null)
                throw new InvalidOperationException($"Could not instantiate '{newPath}'.");

            NodeTargets.RegisterCreated(scope, replacement, "ucp prefab_replace");

            if (parent != null)
                replacement.transform.SetParent(parent, worldPositionStays: false);

            if (keepTransform)
            {
                replacement.transform.localPosition = localPosition;
                replacement.transform.localRotation = localRotation;
                replacement.transform.localScale = localScale;
            }

            replacement.name = name;
            replacement.SetActive(wasActive);

            NodeTargets.Destroy(scope, root);
            replacement.transform.SetSiblingIndex(siblingIndex);
            scope.MarkDirty();

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(replacement.transform),
                ["previous_source"] = oldPath,
                ["source"] = newPath,
                ["kept_transform"] = keepTransform,
                ["overrides_preserved"] = false,
                ["instance_id"] = replacement.GetInstanceID(),
                ["target"] = scope.Describe(),
            };
        }

        public static Dictionary<string, object> Info(EditTargetScope scope, PrefabInstanceParams p)
        {
            var node = NodeTargets.Require(scope, p);
            var go = node.gameObject;

            var result = new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["is_part_of_instance"] = PrefabUtility.IsPartOfPrefabInstance(go),
                ["is_instance_root"] = PrefabUtility.IsAnyPrefabInstanceRoot(go),
                ["is_part_of_asset"] = PrefabUtility.IsPartOfPrefabAsset(go),
                ["target"] = scope.Describe(),
            };

            if (!PrefabUtility.IsPartOfPrefabInstance(go))
            {
                result["source"] = null;
                return result;
            }

            var nearest = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            var outermost = PrefabUtility.GetOutermostPrefabInstanceRoot(go);

            result["source"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            result["nearest_instance_root"] = nearest == null ? null : GameObjectPath.Of(nearest.transform);
            result["outermost_instance_root"] = outermost == null ? null : GameObjectPath.Of(outermost.transform);
            result["status"] = PrefabUtility.GetPrefabInstanceStatus(go).ToString();
            result["is_disconnected"] = PrefabUtility.GetPrefabInstanceStatus(go) != PrefabInstanceStatus.Connected;

            var corresponding = PrefabUtility.GetCorrespondingObjectFromSource(go);
            result["asset_node"] = corresponding == null ? null : GameObjectPath.Of(corresponding.transform);

            if (nearest != null)
                result["overrides"] = CountOverrides(nearest);

            return result;
        }

        // --- shared ------------------------------------------------------------------------

        static (GameObject root, string assetPath) RequireInstance(GameObject node)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(node))
            {
                throw new InvalidOperationException(
                    $"'{GameObjectPath.Of(node.transform)}' is not part of a prefab instance. " +
                    "Create one with prefab_instantiate, or edit the asset directly by passing 'prefab'.");
            }

            var root = PrefabUtility.GetNearestPrefabInstanceRoot(node);
            if (root == null)
                throw new InvalidOperationException($"Could not find the prefab instance root of '{node.name}'.");

            var assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
            if (string.IsNullOrEmpty(assetPath))
            {
                throw new InvalidOperationException(
                    $"'{root.name}' is a prefab instance whose source asset is missing.");
            }

            return (root, assetPath);
        }

        static SerializedProperty RequireProperty(
            GameObject node,
            PrefabOverrideParams p,
            out Component component)
        {
            if (string.IsNullOrWhiteSpace(p.Component))
                throw new ArgumentException("'property' also needs 'component' so the field can be located.");

            component = ComponentService.Require(node, p.Component, p.Index);
            var serialized = new SerializedObject(component);
            var property = SerializedValueWriter.FindProperty(serialized, p.Property);
            if (property == null)
            {
                throw new ArgumentException(
                    $"{component.GetType().Name} has no property '{p.Property}'. Use component_get to list them.");
            }

            return property;
        }

        static Dictionary<string, object> CountOverrides(GameObject root) =>
            new Dictionary<string, object>
            {
                ["any"] = PrefabUtility.HasPrefabInstanceAnyOverrides(root, includeDefaultOverrides: false),
                ["property_modifications"] = (PrefabUtility.GetPropertyModifications(root) ??
                                              Array.Empty<PropertyModification>()).Length,
                ["added_components"] = PrefabUtility.GetAddedComponents(root).Count,
                ["removed_components"] = PrefabUtility.GetRemovedComponents(root).Count,
                ["added_gameobjects"] = PrefabUtility.GetAddedGameObjects(root).Count,
            };

        static string NodeOf(Object target) =>
            target switch
            {
                GameObject go => GameObjectPath.Of(go.transform),
                Component component => GameObjectPath.Of(component.transform),
                _ => null,
            };
    }
}
