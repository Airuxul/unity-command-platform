using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Editor.Services.Assets;
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
        public static Dictionary<string, object> Create(EditTargetScope scope, PrefabCreateParams p)
        {
            var path = AssetPaths.RequirePrefabPath(p.Path, ParamKeys.Path);

            var node = GameObjectPath.Require(scope, p);
            var connect = p.Connect ?? true;

            AssetPaths.RequireNewAssetPath(path, ParamKeys.Path);
            AssetPaths.EnsureParentFolder(path);

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
                ["components"] = ComponentService.Names(asset),
            };
        }

        public static Dictionary<string, object> CreateEmpty(PrefabCreateEmptyParams p)
        {
            var path = AssetPaths.RequirePrefabPath(p.Path, ParamKeys.Path);
            var name = string.IsNullOrWhiteSpace(p.Name)
                ? Path.GetFileNameWithoutExtension(path)
                : p.Name.Trim();

            AssetPaths.RequireNewAssetPath(path, ParamKeys.Path);
            AssetPaths.EnsureParentFolder(path);

            List<string> added;
            GameObject asset;

            using (var staging = PreviewStage.Begin())
            {
                var root = staging.Adopt(Primitives.Create(p.Primitive));
                root.name = name;
                added = Primitives.AddComponents(root, p.Components);

                asset = PrefabUtility.SaveAsPrefabAsset(root, path, out var saved);
                if (!saved || asset == null)
                    throw new InvalidOperationException($"Unity could not save '{path}'.");
            }

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["root"] = name,
                ["primitive"] = Primitives.Name(p.Primitive),
                ["components"] = ComponentService.Names(asset),
                ["components_added"] = added,
            };
        }

        public static Dictionary<string, object> CreateVariant(PrefabVariantParams p)
        {
            var basePath = AssetPaths.RequireExisting(p.Path, ParamKeys.Path);
            var baseAsset = AssetPaths.RequireAsset<GameObject>(basePath, ParamKeys.Path);
            var target = AssetPaths.RequirePrefabPath(p.Target, ParamKeys.Target);

            if (string.Equals(basePath, target, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("A variant cannot be written over its own base prefab.");

            AssetPaths.RequireNewAssetPath(target, ParamKeys.Target);
            AssetPaths.EnsureParentFolder(target);

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

        /// <summary>Copying a non-prefab to a .prefab path would produce a broken asset with a prefab name.</summary>
        static string RequirePrefabSource(string path)
        {
            var source = AssetPaths.RequirePrefabPath(path, ParamKeys.Path);
            var asset = AssetPaths.RequireAsset<GameObject>(source, ParamKeys.Path);
            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab)
                throw new ArgumentException($"'{source}' is not a prefab asset.");

            return source;
        }

        public static Dictionary<string, object> Delete(PrefabPathParams p)
        {
            var path = AssetPaths.RequirePrefabPath(p.Path, ParamKeys.Path);
            AssetPaths.RequireExisting(path, ParamKeys.Path);
            return AssetService.Trash(path);
        }

        public static Dictionary<string, object> Duplicate(PrefabCopyParams p) =>
            AssetService.Copy(RequirePrefabSource(p.Path), AssetPaths.RequirePrefabPath(p.Target, ParamKeys.Target));

        // The GUID is stable across a move — that is the point of moving rather than copying.
        public static Dictionary<string, object> Move(PrefabCopyParams p) =>
            AssetService.Move(RequirePrefabSource(p.Path), AssetPaths.RequirePrefabPath(p.Target, ParamKeys.Target));

        public static Dictionary<string, object> List(PrefabListParams p)
        {
            var includeVariants = p.IncludeVariants ?? true;

            var page = AssetQuery.InFolder(p.Folder, "t:Prefab", p.Name, p.Limit,
                (path, guid) => new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["guid"] = guid,
                    ["name"] = Path.GetFileNameWithoutExtension(path),
                    ["asset_type"] = AssetTypeAt(path).ToString(),
                },
                include: path => includeVariants || AssetTypeAt(path) == PrefabAssetType.Regular);

            return page.Describe("prefabs");
        }

        static PrefabAssetType AssetTypeAt(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return asset == null ? PrefabAssetType.NotAPrefab : PrefabUtility.GetPrefabAssetType(asset);
        }

        public static Dictionary<string, object> Info(PrefabInfoParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, ParamKeys.Path);
            var asset = AssetPaths.RequireAsset<GameObject>(path, ParamKeys.Path);
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
                ["components"] = ComponentService.Names(asset),
                ["transform"] = TransformValues.Describe(asset.transform),
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
            var path = AssetPaths.RequireExisting(p.Path, ParamKeys.Path);

            using var scope = EditTargetScope.Peek(path);
            var tree = GameObjectService.Tree(scope, new GameObjectTreeParams
            {
                Node = p.Node,
                Depth = p.Depth ?? -1,
                Components = p.Components,
                IncludeInactive = true,
                Limit = p.Limit,
                Format = p.Format,
                Fields = p.Fields,
            });
            // Says whether the tree came from the asset or from the open Prefab Mode stage.
            tree["target"] = scope.Describe();
            return tree;
        }
    }
}
