using System;
using System.Collections.Generic;
using UnityEditor;

namespace Air.UcpAgent.Editor.Services
{
    public static class AssetService
    {
        public static Dictionary<string, object> Execute(string action, string path, string to)
        {
            HierarchyEdit.EnsureEditMode();
            action = action?.Trim().ToLowerInvariant();
            if (action == "mkdir")
                return Mkdir(path);
            if (action == "delete")
                return Delete(path);
            if (action == "move")
                return MoveOrCopy(path, to, false);
            if (action == "copy")
                return MoveOrCopy(path, to, true);
            throw new ArgumentException($"Unknown action '{action}'. Use move, copy, delete, or mkdir.");
        }

        static Dictionary<string, object> MoveOrCopy(string path, string to, bool copy)
        {
            path = RequireAsset(path);
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("to is required.");
            to = RequireAsset(to);
            if (string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Cannot move or copy Assets itself.");

            var action = copy ? "copy" : "move";
            bool ok;
            string error = null;
            if (copy)
                ok = AssetDatabase.CopyAsset(path, to);
            else
            {
                error = AssetDatabase.MoveAsset(path, to);
                ok = string.IsNullOrEmpty(error);
            }

            if (!ok)
                throw new InvalidOperationException(string.IsNullOrEmpty(error) ? $"Failed to {action} {path}." : error);

            return new Dictionary<string, object>
            {
                ["action"] = action,
                ["path"] = path,
                ["to"] = to,
            };
        }

        static Dictionary<string, object> Delete(string path)
        {
            path = RequireAsset(path);
            if (string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Cannot delete Assets.");
            if (!AssetDatabase.DeleteAsset(path))
                throw new InvalidOperationException($"Failed to delete {path}.");
            return new Dictionary<string, object>
            {
                ["action"] = "delete",
                ["path"] = path,
            };
        }

        static Dictionary<string, object> Mkdir(string path)
        {
            path = RequireAsset(path);
            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(next) != null)
                        throw new InvalidOperationException($"Cannot create folder over an existing asset: {next}");
                    var guid = AssetDatabase.CreateFolder(current, parts[i]);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException($"Failed to create folder: {next}");
                }

                current = next;
            }

            return new Dictionary<string, object>
            {
                ["action"] = "mkdir",
                ["path"] = path,
            };
        }

        static string RequireAsset(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("path is required.");
            path = path.Replace('\\', '/').Trim().TrimEnd('/');
            if (path.Contains(".."))
                throw new ArgumentException($"Path cannot contain ..: {path}");
            if (!string.Equals(path, "Assets", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Path must stay under Assets/: {path}");
            return path;
        }
    }
}
