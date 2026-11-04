using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.Services.Scenes
{
    /// <summary>
    /// Scene lifecycle. Every destructive operation refuses to run against unsaved changes unless
    /// <c>force</c> is passed — Unity would otherwise raise a modal "save changes?" dialog, which
    /// blocks the Editor main thread and therefore the agent's whole command loop.
    /// </summary>
    public static class SceneService
    {
        const int DefaultListLimit = 200;

        public static Dictionary<string, object> Open(SceneOpenParams p)
        {
            RequireEditMode("open a scene");

            var path = AssetPaths.RequireExisting(p.Path, "path");
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"'{path}' is not a scene asset (expected a .unity file).");

            var mode = ParseOpenMode(p.Mode);
            if (mode == OpenSceneMode.Single)
                RequireNoUnsavedChanges(p.Force, "Opening a scene in single mode closes them.");

            var scene = EditorSceneManager.OpenScene(path, mode);
            if (!scene.IsValid())
                throw new InvalidOperationException($"Unity could not open '{path}'.");

            return Snapshot(new Dictionary<string, object>
            {
                ["path"] = path,
                ["mode"] = mode.ToString(),
                ["name"] = scene.name,
                ["root_count"] = scene.isLoaded ? scene.rootCount : 0,
            });
        }

        public static Dictionary<string, object> New(SceneNewParams p)
        {
            RequireEditMode("create a scene");

            var setup = string.Equals(p.Setup, "empty", StringComparison.OrdinalIgnoreCase)
                ? NewSceneSetup.EmptyScene
                : NewSceneSetup.DefaultGameObjects;
            var mode = string.Equals(p.Mode, "additive", StringComparison.OrdinalIgnoreCase)
                ? NewSceneMode.Additive
                : NewSceneMode.Single;

            string savedTo = null;
            if (!string.IsNullOrWhiteSpace(p.Path))
            {
                savedTo = RequireScenePath(p.Path, "path");
                AssetPaths.RequireNewAssetPath(savedTo, "path");
            }

            if (mode == NewSceneMode.Single)
                RequireNoUnsavedChanges(p.Force, "Creating a scene in single mode closes them.");

            var scene = EditorSceneManager.NewScene(setup, mode);

            if (savedTo != null)
            {
                AssetPaths.EnsureParentFolder(savedTo);
                if (!EditorSceneManager.SaveScene(scene, savedTo))
                    throw new InvalidOperationException($"Unity could not save the new scene to '{savedTo}'.");
            }

            return Snapshot(new Dictionary<string, object>
            {
                ["setup"] = setup.ToString(),
                ["mode"] = mode.ToString(),
                ["name"] = scene.name,
                ["path"] = savedTo,
                ["saved"] = savedTo != null,
            });
        }

        public static Dictionary<string, object> Save(SceneSaveParams p)
        {
            RequireEditMode("save a scene");

            if (p.All)
            {
                // SaveOpenScenes asks for a path in a modal dialog for every untitled scene, which
                // would block the Editor main thread and with it the agent.
                var untitled = LoadedScenes()
                    .Where(s => s["path"] == null && s["dirty"] is true)
                    .Select(s => (string)s["name"])
                    .ToArray();
                if (untitled.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"Never-saved scenes: {string.Join(", ", untitled)}. Save each with scene_save_as first.");
                }

                if (!EditorSceneManager.SaveOpenScenes())
                    throw new InvalidOperationException("Unity could not save the open scenes.");

                return Snapshot(new Dictionary<string, object>
                {
                    ["saved"] = LoadedScenes().Select(s => s["path"]).ToList(),
                    ["scope"] = "all",
                });
            }

            var scene = Resolve(p.Scene);
            if (string.IsNullOrEmpty(scene.path))
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' has never been saved. Use scene_save_as with a path.");
            }

            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException($"Unity could not save '{scene.path}'.");

            return Snapshot(new Dictionary<string, object>
            {
                ["saved"] = new List<object> { scene.path },
                ["scope"] = "scene",
                ["path"] = scene.path,
            });
        }

        public static Dictionary<string, object> SaveAs(SceneSaveAsParams p)
        {
            RequireEditMode("save a scene");

            var path = RequireScenePath(p.Path, "path");
            AssetPaths.RequireNewAssetPath(path, "path");
            var scene = Resolve(p.Scene);
            var previous = scene.path;

            AssetPaths.EnsureParentFolder(path);
            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException($"Unity could not save the scene to '{path}'.");

            return Snapshot(new Dictionary<string, object>
            {
                ["path"] = path,
                ["previous_path"] = string.IsNullOrEmpty(previous) ? null : previous,
                ["guid"] = AssetDatabase.AssetPathToGUID(path),
            });
        }

        public static Dictionary<string, object> Close(SceneCloseParams p)
        {
            RequireEditMode("close a scene");

            var scene = Resolve(p.Scene);
            if (SceneManager.sceneCount <= 1)
            {
                throw new InvalidOperationException(
                    "Cannot close the only open scene. Use scene_open or scene_new to replace it instead.");
            }

            if (scene.isDirty && !p.Force)
            {
                throw new InvalidOperationException(
                    $"Scene '{scene.name}' has unsaved changes. Save it with scene_save, or pass force=true to discard them.");
            }

            var path = scene.path;
            var name = scene.name;
            if (!EditorSceneManager.CloseScene(scene, p.Remove ?? true))
                throw new InvalidOperationException($"Unity could not close '{name}'.");

            return Snapshot(new Dictionary<string, object>
            {
                ["closed"] = name,
                ["path"] = string.IsNullOrEmpty(path) ? null : path,
                ["removed"] = p.Remove ?? true,
            });
        }

        public static Dictionary<string, object> List(SceneListParams p)
        {
            var folder = string.IsNullOrWhiteSpace(p.Folder)
                ? "Assets"
                : AssetPaths.RequireProjectPath(p.Folder, "folder");

            if (!AssetDatabase.IsValidFolder(folder))
                throw new ArgumentException($"'{folder}' is not a folder.");

            var limit = p.Limit ?? DefaultListLimit;
            var assets = new List<object>();
            var total = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!string.IsNullOrWhiteSpace(p.Name) &&
                    Path.GetFileNameWithoutExtension(path).IndexOf(p.Name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                total++;
                if (assets.Count >= limit)
                    continue;

                assets.Add(new Dictionary<string, object>
                {
                    ["path"] = path,
                    ["guid"] = guid,
                    ["name"] = Path.GetFileNameWithoutExtension(path),
                    ["in_build"] = EditorBuildSettings.scenes.Any(s =>
                        string.Equals(s.path, path, StringComparison.OrdinalIgnoreCase)),
                });
            }

            return Snapshot(new Dictionary<string, object>
            {
                ["folder"] = folder,
                ["count"] = assets.Count,
                ["total_matches"] = total,
                ["truncated"] = total > assets.Count,
                ["scenes"] = assets,
            });
        }

        public static Dictionary<string, object> Info() => Snapshot(new Dictionary<string, object>());

        // --- shared ------------------------------------------------------------------------

        /// <summary>Every scene payload ends with the same live picture of what is open.</summary>
        static Dictionary<string, object> Snapshot(Dictionary<string, object> body)
        {
            var active = SceneManager.GetActiveScene();
            body["active_scene"] = active.IsValid()
                ? new Dictionary<string, object>
                {
                    ["name"] = active.name,
                    ["path"] = string.IsNullOrEmpty(active.path) ? null : active.path,
                    ["dirty"] = active.isDirty,
                    ["loaded"] = active.isLoaded,
                    ["root_count"] = active.isLoaded ? active.rootCount : 0,
                }
                : null;
            body["loaded_scenes"] = LoadedScenes();
            return body;
        }

        static List<Dictionary<string, object>> LoadedScenes()
        {
            var scenes = new List<Dictionary<string, object>>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                scenes.Add(new Dictionary<string, object>
                {
                    ["name"] = scene.name,
                    ["path"] = string.IsNullOrEmpty(scene.path) ? null : scene.path,
                    ["dirty"] = scene.isDirty,
                    ["loaded"] = scene.isLoaded,
                    ["root_count"] = scene.isLoaded ? scene.rootCount : 0,
                });
            }

            return scenes;
        }

        static Scene Resolve(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                var active = SceneManager.GetActiveScene();
                if (!active.IsValid())
                    throw new InvalidOperationException("No scene is loaded.");

                return active;
            }

            var wanted = AssetPaths.Normalize(nameOrPath);
            var loaded = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                loaded.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
                if (string.Equals(scene.name, wanted, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(AssetPaths.Normalize(scene.path), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }

            throw new ArgumentException(
                $"Scene '{nameOrPath}' is not loaded. Loaded: {(loaded.Count == 0 ? "(none)" : string.Join(", ", loaded))}.");
        }

        static OpenSceneMode ParseOpenMode(string mode)
        {
            var value = string.IsNullOrWhiteSpace(mode) ? "single" : mode.Trim().ToLowerInvariant();
            return value switch
            {
                "single" => OpenSceneMode.Single,
                "additive" => OpenSceneMode.Additive,
                "additive_without_loading" or "additivewithoutloading" => OpenSceneMode.AdditiveWithoutLoading,
                _ => throw new ArgumentException(
                    $"Unknown mode '{mode}'. Valid: single, additive, additive_without_loading."),
            };
        }

        static string RequireScenePath(string path, string paramName)
        {
            var normalized = AssetPaths.RequireProjectPath(path, paramName);
            if (!normalized.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Parameter '{paramName}' must end with '.unity', got '{normalized}'.");

            return normalized;
        }

        static void RequireEditMode(string action)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException($"Cannot {action} in play mode. Run 'stop' first.");
        }

        static void RequireNoUnsavedChanges(bool force, string consequence)
        {
            // An open Prefab Mode stage counts: changing scenes leaves the stage, and Unity prompts
            // for unsaved prefab changes with a modal that would block the agent's poll loop.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty)
            {
                if (!force)
                {
                    throw new InvalidOperationException(
                        $"'{stage.assetPath}' is open in Prefab Mode with unsaved changes. " +
                        "Run prefab_save or prefab_close first, or pass force=true to discard them.");
                }

                stage.ClearDirtiness();
            }

            if (force)
                return;

            var dirty = LoadedScenes()
                .Where(s => s["dirty"] is true)
                .Select(s => (string)(s["path"] ?? s["name"]))
                .ToArray();

            if (dirty.Length == 0)
                return;

            throw new InvalidOperationException(
                $"Unsaved changes in: {string.Join(", ", dirty)}. {consequence} " +
                "Save with scene_save, or pass force=true to discard.");
        }
    }
}
