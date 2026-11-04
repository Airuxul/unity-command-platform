using System;
using System.Collections.Generic;
using System.IO;
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
    /// Prefab assets on disk: create, variant, copy, move, delete, list, inspect.
    /// Editing what is <em>inside</em> a prefab goes through
    /// <see cref="Objects.GameObjectService"/> / <see cref="Objects.ComponentService"/> with the
    /// <c>prefab</c> argument instead.
    /// </summary>
    public static class PrefabAssetService
    {
        const int DefaultListLimit = 200;

        public static Dictionary<string, object> Create(PrefabCreateParams p)
        {
            var path = AssetPaths.RequirePrefabPath(p.Path, "path");

            using var scope = NodeTargets.Open(p);
            var node = NodeTargets.Require(scope, p);
            var connect = p.Connect ?? true;

            AssetPaths.EnsureParentFolder(path);
            AssetPaths.RequireFreeOrOverwrite(path, p.Overwrite);

            GameObject asset;
            bool saved;
            if (connect)
            {
                // Leaves the source node as an instance of the new prefab — inside another prefab's
                // contents that is exactly how a nested prefab is created.
                asset = PrefabUtility.SaveAsPrefabAssetAndConnect(
                    node.gameObject, path, InteractionMode.AutomatedAction, out saved);
                scope.MarkDirty();
            }
            else
            {
                asset = PrefabUtility.SaveAsPrefabAsset(node.gameObject, path, out saved);
            }

            if (!saved || asset == null)
                throw new InvalidOperationException($"Unity could not save '{path}'.");

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["source_node"] = GameObjectPath.Of(node),
                ["root"] = asset.name,
                ["connected"] = connect,
                ["components"] = NodeTargets.ComponentNames(asset),
                ["target"] = scope.Describe(),
            };
        }

        public static Dictionary<string, object> CreateEmpty(PrefabCreateEmptyParams p)
        {
            var path = AssetPaths.RequirePrefabPath(p.Path, "path");
            var name = string.IsNullOrWhiteSpace(p.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : p.Name.Trim();

            AssetPaths.EnsureParentFolder(path);
            AssetPaths.RequireFreeOrOverwrite(path, p.Overwrite);

            var added = new List<string>();
            GameObject asset;

            using (var staging = PreviewStage.Begin())
            {
                var root = staging.Adopt(Primitives.Create(p.Primitive));
                root.name = name;

                foreach (var typeName in p.Components ?? Array.Empty<string>())
                {
                    if (string.IsNullOrWhiteSpace(typeName))
                        continue;

                    var type = ComponentTypeResolver.RequireComponent(typeName);
                    if (root.GetComponent(type) == null && root.AddComponent(type) == null)
                        throw new InvalidOperationException($"Could not add {type.Name} to '{name}'.");

                    added.Add(type.Name);
                }

                asset = PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved || asset == null)
                    throw new InvalidOperationException($"Unity could not save '{path}'.");
            }

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["root"] = name,
                ["primitive"] = string.IsNullOrWhiteSpace(p.Primitive) ? "empty" : p.Primitive.ToLowerInvariant(),
                ["components"] = NodeTargets.ComponentNames(asset),
                ["components_added"] = added,
            };
        }

        public static Dictionary<string, object> CreateVariant(PrefabVariantParams p)
        {
            var basePath = AssetPaths.RequireExisting(p.Path, "path");
            var baseAsset = AssetPaths.RequireAsset<GameObject>(basePath, "path");
            var target = AssetPaths.RequirePrefabPath(p.Target, "target");

            if (string.Equals(basePath, target, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A variant cannot be written over its own base prefab.");

            AssetPaths.EnsureParentFolder(target);
            AssetPaths.RequireFreeOrOverwrite(target, p.Overwrite);

            GameObject variant;
            using (var staging = PreviewStage.Begin())
            {
                // Saving a *prefab instance* as a new asset is what makes Unity write a variant.
                var instance = PrefabUtility.InstantiatePrefab(baseAsset, staging.Scene) as GameObject;
                if (instance == null)
                    throw new InvalidOperationException($"Could not instantiate '{basePath}'.");

                staging.Track(instance);
                variant = PrefabUtility.SaveAsPrefabAsset(instance, target, out var saved);
                if (!saved || variant == null)
                    throw new InvalidOperationException($"Unity could not save variant '{target}'.");
            }

            var assetType = PrefabUtility.GetPrefabAssetType(variant);
            return new Dictionary<string, object>
            {
                ["path"] = target,
                ["guid"] = AssetDatabase.AssetPathToGUID(target),
                ["base"] = basePath,
                ["root"] = variant.name,
                ["asset_type"] = assetType.ToString(),
                ["is_variant"] = assetType == PrefabAssetType.Variant,
            };
        }

        public static Dictionary<string, object> Delete(PrefabPathParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, "path");
            PrefabStageService.RequireNotOpenInStage(path, "deleted");
            var guid = AssetDatabase.AssetPathToGUID(path);

            if (!AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Could not delete '{path}'.");

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = guid,
                ["deleted"] = true,
            };
        }

        public static Dictionary<string, object> Duplicate(PrefabCopyParams p)
        {
            var source = AssetPaths.RequireExisting(p.Path, "path");
            var target = AssetPaths.RequirePrefabPath(p.Target, "target");

            AssetPaths.EnsureParentFolder(target);
            AssetPaths.RequireFreeOrOverwrite(target, p.Overwrite);

            if (!AssetDatabase.CopyAsset(source, target))
                throw new InvalidOperationException($"Could not copy '{source}' to '{target}'.");

            AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);

            return new Dictionary<string, object>
            {
                ["source"] = source,
                ["path"] = target,
                ["guid"] = AssetDatabase.AssetPathToGUID(target),
            };
        }

        public static Dictionary<string, object> Move(PrefabCopyParams p)
        {
            var source = AssetPaths.RequireExisting(p.Path, "path");
            PrefabStageService.RequireNotOpenInStage(source, "moved");
            var target = AssetPaths.RequirePrefabPath(p.Target, "target");

            AssetPaths.EnsureParentFolder(target);
            AssetPaths.RequireFreeOrOverwrite(target, p.Overwrite);

            var error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Could not move '{source}' to '{target}': {error}");

            return new Dictionary<string, object>
            {
                ["source"] = source,
                ["path"] = target,
                // The GUID is stable across a move — that is the point of moving rather than copying.
                ["guid"] = AssetDatabase.AssetPathToGUID(target),
            };
        }

        public static Dictionary<string, object> List(PrefabListParams p)
        {
            var folder = string.IsNullOrWhiteSpace(p.Folder)
                ? "Assets"
                : AssetPaths.RequireProjectPath(p.Folder, "folder");

            if (!AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException($"'{folder}' is not a folder.");

            var limit = p.Limit ?? DefaultListLimit;
            var includeVariants = p.IncludeVariants ?? true;

            var results = new List<object>();
            var total = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!string.IsNullOrWhiteSpace(p.Name) &&
                    Path.GetFileNameWithoutExtension(path).IndexOf(p.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var assetType = asset == null ? PrefabAssetType.NotAPrefab : PrefabUtility.GetPrefabAssetType(asset);
                if (!includeVariants && assetType != PrefabAssetType.Regular)
                    continue;

                total++;
                if (results.Count >= limit)
                    continue;

                results.Add(new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["guid"] = guid,
                    ["name"] = Path.GetFileNameWithoutExtension(path),
                    ["asset_type"] = assetType.ToString(),
                });
            }

            return new Dictionary<string, object>
            {
                ["folder"] = folder,
                ["count"] = results.Count,
                ["total_matches"] = total,
                ["truncated"] = total > results.Count,
                ["prefabs"] = results,
            };
        }

        public static Dictionary<string, object> Info(PrefabInfoParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, "path");
            var asset = AssetPaths.RequireAsset<GameObject>(path, "path");
            var assetType = PrefabUtility.GetPrefabAssetType(asset);

            var result = new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["root"] = asset.name,
                ["asset_type"] = assetType.ToString(),
                ["editable"] = assetType != PrefabAssetType.Model,
                ["tag"] = asset.tag,
                ["layer_name"] = LayerMask.LayerToName(asset.layer),
                ["child_count"] = asset.transform.childCount,
                ["descendant_count"] = GameObjectPath.Descend(asset.transform).Count() - 1,
                ["components"] = NodeTargets.ComponentNames(asset),
                ["transform"] = GameObjectService.DescribeTransform(asset.transform),
            };

            if (assetType == PrefabAssetType.Variant)
            {
                var source = PrefabUtility.GetCorrespondingObjectFromSource(asset);
                result["base_prefab"] = source == null ? null : AssetDatabase.GetAssetPath(source);
            }

            result["nested_prefabs"] = GameObjectPath.Descend(asset.transform)
                .Where(t => t != asset.transform && PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject))
                .Select(t => new Dictionary<string, object>
                {
                    ["node"] = GameObjectPath.Of(t),
                    ["source"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),
                })
                .Cast<object>()
                .ToList();

            if (p.Dependencies)
            {
                result["dependencies"] = AssetDatabase.GetDependencies(path, recursive: false)
                    .Where(d => !string.Equals(d, path, StringComparison.Ordinal))
                    .ToList();
            }

            return result;
        }

        /// <summary>
        /// Full content hierarchy. Delegates to <see cref="GameObjectService.Tree"/> so a prefab tree
        /// and a scene tree always report the same fields.
        /// </summary>
        public static Dictionary<string, object> Tree(PrefabTreeParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, "path");

            return GameObjectService.Tree(new GameObjectTreeParams
            {
                Prefab = path,
                Depth = p.Depth ?? -1,
                Components = p.Components,
                IncludeInactive = true,
            });
        }

    }
}
