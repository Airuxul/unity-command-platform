using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.Services
{
    public static class SceneEditService
    {
        public static Dictionary<string, object> Read(string scenePath, string nodePath = null)
        {
            HierarchyEdit.EnsureEditMode();
            var scene = OpenIfNeeded(scenePath);
            var data = SceneHeader(scene);
            if (string.IsNullOrWhiteSpace(nodePath))
            {
                data["nodes"] = HierarchyEdit.FlattenScene();
                return data;
            }

            var node = HierarchyEdit.FindScene(nodePath);
            if (node == null)
                throw new InvalidOperationException(
                    $"Node not found: {nodePath}. Run scene.read and pass a path from that list.");
            data["node"] = HierarchyEdit.Describe(node, nodePath);
            return data;
        }

        public static Dictionary<string, object> Edit(string scenePath, object opsValue)
        {
            HierarchyEdit.EnsureEditMode();
            var scene = OpenIfNeeded(scenePath);
            if (string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Save the scene as an asset before scene.edit.");

            var wasDirty = scene.isDirty;
            var scenePathOnDisk = scene.path;
            List<Dictionary<string, object>> done;
            try
            {
                done = HierarchyEdit.ApplyAll(HierarchyTarget.Scene(), opsValue);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene))
                    throw new InvalidOperationException($"Failed to save scene: {scene.path}");
            }
            catch (Exception)
            {
                if (!wasDirty)
                {
                    try
                    {
                        EditorSceneManager.OpenScene(scenePathOnDisk, OpenSceneMode.Single);
                    }
                    catch (Exception)
                    {
                        // Keep the edit error. A failed reload leaves the scene dirty.
                    }
                }

                throw;
            }

            var data = SceneHeader(scene);
            data["done"] = done;
            return data;
        }

        static Scene OpenIfNeeded(string scenePath)
        {
            var active = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                if (!active.IsValid())
                    throw new InvalidOperationException("No active scene.");
                return active;
            }

            scenePath = scenePath.Replace('\\', '/');
            if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"scene_path must end with .unity: {scenePath}");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                throw new InvalidOperationException($"Scene not found: {scenePath}");
            if (string.Equals(active.path.Replace('\\', '/'), scenePath, StringComparison.OrdinalIgnoreCase))
                return active;
            if (active.isDirty)
                throw new InvalidOperationException(
                    "Active scene has unsaved changes. Save it in the Editor before opening another scene.");

            var opened = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!opened.IsValid())
                throw new InvalidOperationException($"Failed to open scene: {scenePath}");
            return opened;
        }

        static Dictionary<string, object> SceneHeader(Scene scene)
        {
            return new Dictionary<string, object>
            {
                ["scene_path"] = scene.path.Replace('\\', '/'),
                ["scene_name"] = scene.name,
            };
        }
    }
}
