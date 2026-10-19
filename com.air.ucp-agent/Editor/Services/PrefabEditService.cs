using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services
{
    public static class PrefabEditService
    {
        public static Dictionary<string, object> Read(string prefabPath, string nodePath = null)
        {
            prefabPath = Normalize(prefabPath);
            HierarchyEdit.EnsureEditMode();
            var stageOpen = StageOpen(prefabPath);
            var root = LoadContents(prefabPath);
            try
            {
                var data = new Dictionary<string, object>
                {
                    ["prefab_path"] = prefabPath,
                    ["prefab_stage_open"] = stageOpen,
                };
                if (string.IsNullOrWhiteSpace(nodePath))
                {
                    data["nodes"] = HierarchyEdit.Flatten(root.transform);
                    return data;
                }

                var node = HierarchyEdit.Find(root.transform, nodePath);
                if (node == null)
                    throw new InvalidOperationException(
                        $"Node not found: {nodePath}. Run prefab.read and pass a path from that list.");
                data["node"] = HierarchyEdit.Describe(node, nodePath);
                return data;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static Dictionary<string, object> Edit(string prefabPath, object opsValue)
        {
            prefabPath = Normalize(prefabPath);
            HierarchyEdit.EnsureEditMode();
            if (StageOpen(prefabPath))
                throw new InvalidOperationException(
                    "This prefab is open in Prefab Mode. Exit Prefab Mode before prefab.edit, or the two saves will overwrite each other.");

            var root = LoadContents(prefabPath);
            try
            {
                var done = HierarchyEdit.ApplyAll(HierarchyTarget.Prefab(root.transform), opsValue);
                EditorUtility.SetDirty(root);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var success);
                if (!success || saved == null)
                    throw new InvalidOperationException($"Failed to save prefab: {prefabPath}");
                return new Dictionary<string, object>
                {
                    ["prefab_path"] = prefabPath,
                    ["done"] = done,
                };
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject LoadContents(string prefabPath)
        {
            if (!prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"prefab_path must end with .prefab: {prefabPath}");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
                throw new InvalidOperationException($"Prefab not found: {prefabPath}");
            if (PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.NotAPrefab)
                throw new InvalidOperationException($"Not a prefab asset: {prefabPath}");
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new InvalidOperationException($"Failed to load prefab: {prefabPath}");
            return root;
        }

        static string Normalize(string prefabPath)
        {
            if (string.IsNullOrWhiteSpace(prefabPath))
                throw new ArgumentException("prefab_path is required.");
            return prefabPath.Replace('\\', '/');
        }

        static bool StageOpen(string prefabPath)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            return stage != null &&
                   string.Equals(stage.assetPath.Replace('\\', '/'), prefabPath, StringComparison.OrdinalIgnoreCase);
        }
    }
}
