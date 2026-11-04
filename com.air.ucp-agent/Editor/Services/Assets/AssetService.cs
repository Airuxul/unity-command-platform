using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using UnityEditor;

namespace Air.UcpAgent.Editor.Services.Assets
{
    /// <summary>
    /// AssetDatabase operations: list, search, inspect, create folders, delete, move, copy,
    /// dependencies, and GUID ↔ path conversion.
    /// </summary>
    public static class AssetService
    {
        const int DefaultLimit = 200;

        public static Dictionary<string, object> List(AssetListParams p)
        {
            var folder = string.IsNullOrWhiteSpace(p.Folder)
                ? "Assets"
                : AssetPaths.RequireProjectPath(p.Folder, "folder");

            if (!AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException($"'{folder}' is not a folder.");

            var recursive = p.Recursive ?? true;
            var limit = p.Limit ?? DefaultLimit;
            var filter = string.IsNullOrWhiteSpace(p.Type) ? string.Empty : "t:" + p.Type.Trim();

            var assets = new List<object>();
            var folders = new List<object>();
            var total = 0;

            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                var parent = AssetPaths.ParentFolder(path);
                if (!recursive && !string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (AssetDatabase.IsValidFolder(path))
                {
                    folders.Add(path);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(p.Name) &&
                    Path.GetFileNameWithoutExtension(path).IndexOf(p.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                total++;
                if (assets.Count >= limit)
                    continue;

                assets.Add(Summary(path, guid));
            }

            return new Dictionary<string, object>
            {
                ["folder"] = folder,
                ["recursive"] = recursive,
                ["count"] = assets.Count,
                ["total_matches"] = total,
                ["truncated"] = total > assets.Count,
                ["assets"] = assets,
                ["folders"] = folders,
            };
        }

        public static Dictionary<string, object> Find(AssetFindParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Filter))
                throw new ArgumentException("Parameter 'filter' is required, e.g. \"t:Material\".");

            var folders = (p.Folders ?? Array.Empty<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => AssetPaths.RequireProjectPath(f, "folders"))
                .ToArray();

            var limit = p.Limit ?? DefaultLimit;
            var guids = folders.Length == 0
                ? AssetDatabase.FindAssets(p.Filter)
                : AssetDatabase.FindAssets(p.Filter, folders);

            var assets = guids
                .Take(limit)
                .Select(guid => Summary(AssetDatabase.GUIDToAssetPath(guid), guid))
                .Cast<object>()
                .ToList();

            return new Dictionary<string, object>
            {
                ["filter"] = p.Filter,
                ["folders"] = folders,
                ["count"] = assets.Count,
                ["total_matches"] = guids.Length,
                ["truncated"] = guids.Length > assets.Count,
                ["assets"] = assets,
            };
        }

        public static Dictionary<string, object> Info(AssetInfoParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, "path");
            var isFolder = AssetDatabase.IsValidFolder(path);
            var main = AssetDatabase.LoadMainAssetAtPath(path);

            var result = new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["name"] = isFolder ? AssetPaths.FileName(path) : Path.GetFileNameWithoutExtension(path),
                ["is_folder"] = isFolder,
                ["type"] = main == null ? null : main.GetType().Name,
                ["main_type"] = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName,
                ["importer"] = AssetImporter.GetAtPath(path)?.GetType().Name,
                ["labels"] = main == null ? Array.Empty<string>() : AssetDatabase.GetLabels(main),
            };

            if (p.SubAssets && !isFolder)
            {
                result["sub_assets"] = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                    .Where(a => a != null)
                    .Select(a => new Dictionary<string, object>
                    {
                        ["name"] = a.name,
                        ["type"] = a.GetType().Name,
                    })
                    .Cast<object>()
                    .ToList();
            }

            if (p.Dependencies)
            {
                result["dependencies"] = AssetDatabase.GetDependencies(path, recursive: false)
                    .Where(d => !string.Equals(d, path, StringComparison.Ordinal))
                    .ToList();
            }

            return result;
        }

        public static Dictionary<string, object> CreateFolder(AssetCreateFolderParams p)
        {
            var path = AssetPaths.RequireProjectPath(p.Path, "path");
            AssetPaths.RequireUnderAssets(path, "path");
            var existed = AssetDatabase.IsValidFolder(path);

            AssetPaths.EnsureFolder(path);

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
                ["created"] = !existed,
                ["already_existed"] = existed,
            };
        }

        public static Dictionary<string, object> Delete(AssetDeleteParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, "path");
            AssetPaths.RequireUnderAssets(path, "path");
            // Covers deleting a folder that contains the prefab currently open in Prefab Mode.
            Prefab.PrefabStageService.RequireNotOpenInStage(path, "deleted");
            var guid = AssetDatabase.AssetPathToGUID(path);
            var wasFolder = AssetDatabase.IsValidFolder(path);

            AssetPaths.Trash(path);

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = guid,
                ["was_folder"] = wasFolder,
                ["trashed"] = true,
            };
        }

        public static Dictionary<string, object> Move(AssetMoveParams p)
        {
            var source = AssetPaths.RequireExisting(p.Path, "path");
            AssetPaths.RequireUnderAssets(source, "path");
            Prefab.PrefabStageService.RequireNotOpenInStage(source, "moved");
            var target = AssetPaths.RequireProjectPath(p.Target, "target");

            AssetPaths.RequireNewAssetPath(target, "target");
            AssetPaths.EnsureParentFolder(target);

            var error = AssetDatabase.MoveAsset(source, target);
            if (!string.IsNullOrEmpty(error))
                throw new InvalidOperationException($"Could not move '{source}' to '{target}': {error}");

            return new Dictionary<string, object>
            {
                ["source"] = source,
                ["path"] = target,
                // Moving keeps the GUID, so every reference to this asset follows it.
                ["guid"] = AssetDatabase.AssetPathToGUID(target),
            };
        }

        public static Dictionary<string, object> Duplicate(AssetMoveParams p)
        {
            var source = AssetPaths.RequireExisting(p.Path, "path");
            var target = AssetPaths.RequireProjectPath(p.Target, "target");

            AssetPaths.RequireNewAssetPath(target, "target");
            AssetPaths.EnsureParentFolder(target);

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

        public static Dictionary<string, object> Dependencies(AssetDependenciesParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, "path");
            var dependencies = AssetDatabase.GetDependencies(path, p.Recursive)
                .Where(d => !string.Equals(d, path, StringComparison.Ordinal))
                .ToList();

            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["recursive"] = p.Recursive,
                ["count"] = dependencies.Count,
                ["dependencies"] = dependencies,
            };
        }

        public static Dictionary<string, object> Guid(AssetGuidParams p)
        {
            var hasPath = !string.IsNullOrWhiteSpace(p.Path);
            var hasGuid = !string.IsNullOrWhiteSpace(p.Guid);

            if (hasPath == hasGuid)
                throw new ArgumentException("Pass exactly one of 'path' or 'guid'.");

            if (hasPath)
            {
                var path = AssetPaths.RequireExisting(p.Path, "path");
                return new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["guid"] = AssetDatabase.AssetPathToGUID(path),
                    ["direction"] = "path_to_guid",
                };
            }

            var resolved = AssetDatabase.GUIDToAssetPath(p.Guid.Trim());
            if (string.IsNullOrEmpty(resolved))
                throw new ArgumentException($"No asset for guid '{p.Guid}'.");

            return new Dictionary<string, object>
            {
                ["guid"] = p.Guid.Trim(),
                ["path"] = resolved,
                ["direction"] = "guid_to_path",
            };
        }

        static Dictionary<string, object> Summary(string path, string guid)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return new Dictionary<string, object>
            {
                ["path"] = path,
                ["guid"] = guid,
                ["name"] = Path.GetFileNameWithoutExtension(path),
                ["type"] = type?.Name,
            };
        }
    }
}
