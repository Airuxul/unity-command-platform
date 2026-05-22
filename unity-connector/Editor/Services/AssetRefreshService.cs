using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine.SceneManagement;

namespace UnityCliConnector.Editor.Services
{
    public static class AssetRefreshService
    {
        public static Dictionary<string, object> Refresh(Dictionary<string, object> parameters)
        {
            var mode = GetString(parameters, "mode", "if_dirty");
            var force = GetBool(parameters, "force");
            var compile = GetBool(parameters, "compile");

            if (!force && EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Cannot refresh while Unity is in or entering play mode. Exit play mode first, or pass force=true.");
            }

            var options = string.Equals(mode, "force", StringComparison.OrdinalIgnoreCase)
                ? ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport
                : ImportAssetOptions.ForceSynchronousImport;

            AssetDatabase.Refresh(options);

            var compileRequested = false;
            if (compile)
            {
                CompilationPipeline.RequestScriptCompilation();
                compileRequested = true;
            }

            var dirtyScenes = GetDirtyOpenScenes();
            string warning = null;
            if (dirtyScenes.Count > 0)
            {
                warning =
                    "Open scenes have unsaved changes. Unity may show a modal if scenes changed on disk.";
            }

            return new Dictionary<string, object>
            {
                ["refreshed"] = true,
                ["compile_requested"] = compileRequested,
                ["force"] = force,
                ["mode"] = mode,
                ["warning"] = warning,
                ["dirty_scenes"] = dirtyScenes,
            };
        }

        private static List<string> GetDirtyOpenScenes()
        {
            var scenes = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty)
                    continue;
                scenes.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
            }

            return scenes;
        }

        private static string GetString(Dictionary<string, object> parameters, string key, string defaultValue)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var raw) || raw == null)
                return defaultValue;
            return raw.ToString();
        }

        private static bool GetBool(Dictionary<string, object> parameters, string key)
        {
            if (parameters == null || !parameters.TryGetValue(key, out var raw) || raw == null)
                return false;
            if (raw is bool b)
                return b;
            return bool.TryParse(raw.ToString(), out var parsed) && parsed;
        }
    }
}
