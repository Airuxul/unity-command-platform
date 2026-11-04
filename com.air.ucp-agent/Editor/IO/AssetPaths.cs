using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor.IO
{
    /// <summary>
    /// Asset path hygiene shared by every content command. Callers pass whatever the CLI gave them;
    /// these helpers normalise it and fail loudly rather than letting AssetDatabase silently no-op
    /// on a backslash path or a path outside the project.
    /// </summary>
    public static class AssetPaths
    {
        /// <summary>Slashes normalised, trimmed, trailing separator removed. Does not validate.</summary>
        public static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var normalized = path.Trim().Replace('\\', '/');
            while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
                normalized = normalized.Substring(0, normalized.Length - 1);

            return normalized;
        }

        /// <summary>Normalises and requires a project-relative path under Assets/ or Packages/.</summary>
        public static string RequireProjectPath(string path, string paramName)
        {
            var normalized = Normalize(path);
            if (string.IsNullOrEmpty(normalized))
                throw new ArgumentException($"Parameter '{paramName}' is required.");

            if (Path.IsPathRooted(normalized))
            {
                var projectRoot = Normalize(Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
                if (normalized.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    normalized = normalized.Substring(projectRoot.Length + 1);
            }

            if (!normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(normalized, "Assets", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    $"Parameter '{paramName}' must be a project-relative path under Assets/ or Packages/, got '{path}'.");
            }

            return normalized;
        }

        /// <summary>Normalises and requires the path to exist as an asset.</summary>
        public static string RequireExisting(string path, string paramName)
        {
            var normalized = RequireProjectPath(path, paramName);
            if (!Exists(normalized))
                throw new ArgumentException($"Asset not found at '{normalized}'.");

            return normalized;
        }

        /// <summary>
        /// Whether an asset is really there. <c>AssetPathToGUID</c> defaults to
        /// <c>IncludeRecentlyDeletedAssets</c>, so a plain GUID lookup keeps reporting a deleted
        /// asset as present — which turns a create-delete-create cycle into a bogus
        /// "already exists" failure.
        /// </summary>
        public static bool Exists(string normalizedPath)
        {
            if (string.IsNullOrEmpty(normalizedPath))
                return false;

            if (AssetDatabase.IsValidFolder(normalizedPath))
                return true;

            var guid = AssetDatabase.AssetPathToGUID(
                normalizedPath,
                AssetPathToGUIDOptions.OnlyExistingAssets);
            return guid.Length > 0;
        }

        /// <summary>Loads an asset, throwing a message that names both the path and the wanted type.</summary>
        public static T RequireAsset<T>(string path, string paramName) where T : UnityEngine.Object
        {
            var normalized = RequireExisting(path, paramName);
            var asset = AssetDatabase.LoadAssetAtPath<T>(normalized);
            if (asset == null)
                throw new ArgumentException($"Asset at '{normalized}' is not a {typeof(T).Name}.");

            return asset;
        }

        /// <summary>Creates every missing folder above <paramref name="assetPath"/>.</summary>
        public static void EnsureParentFolder(string assetPath)
        {
            var parent = ParentFolder(assetPath);
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
        }

        /// <summary>Creates <paramref name="folderPath"/> and any missing ancestor folders.</summary>
        public static void EnsureFolder(string folderPath)
        {
            var normalized = Normalize(folderPath);
            if (string.IsNullOrEmpty(normalized) || AssetDatabase.IsValidFolder(normalized))
                return;

            var segments = normalized.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = current + "/" + segments[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    var guid = AssetDatabase.CreateFolder(current, segments[i]);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException($"Could not create folder '{next}'.");
                }

                current = next;
            }
        }

        public static string ParentFolder(string assetPath)
        {
            var normalized = Normalize(assetPath);
            if (string.IsNullOrEmpty(normalized))
                return null;

            var index = normalized.LastIndexOf('/');
            return index <= 0 ? null : normalized.Substring(0, index);
        }

        public static string FileName(string assetPath)
        {
            var normalized = Normalize(assetPath);
            if (string.IsNullOrEmpty(normalized))
                return null;

            var index = normalized.LastIndexOf('/');
            return index < 0 ? normalized : normalized.Substring(index + 1);
        }

        /// <summary>Normalises a target path and requires a <c>.prefab</c> extension.</summary>
        public static string RequirePrefabPath(string path, string paramName)
        {
            var normalized = RequireProjectPath(path, paramName);
            if (!normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Parameter '{paramName}' must end with '.prefab', got '{normalized}'.");

            return normalized;
        }

        /// <summary>Refuses to overwrite unless the caller opted in.</summary>
        public static void RequireFreeOrOverwrite(string normalizedPath, bool overwrite)
        {
            if (!Exists(normalizedPath))
                return;

            if (!overwrite)
            {
                throw new InvalidOperationException(
                    $"'{normalizedPath}' already exists. Pass overwrite=true to replace it.");
            }

            if (!AssetDatabase.DeleteAsset(normalizedPath))
                throw new InvalidOperationException($"Could not delete existing asset '{normalizedPath}'.");
        }
    }
}
