using System;
using System.Collections.Generic;
using Air.UcpAgent.Editor.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.Services.Objects
{
    public enum EditTargetKind
    {
        /// <summary>A loaded scene in the main stage.</summary>
        Scene,

        /// <summary>A prefab asset opened off-screen via <c>PrefabUtility.LoadPrefabContents</c>.</summary>
        PrefabAsset,

        /// <summary>The prefab currently open in Prefab Mode.</summary>
        PrefabStage,
    }

    /// <summary>
    /// The one edit target abstraction behind every node/component command: a set of root
    /// GameObjects plus the right way to persist changes to them. This is what lets
    /// <c>gameobject_*</c> / <c>component_*</c> exist once instead of twice — pass a prefab path
    /// and they edit the asset, pass nothing and they edit the open scene.
    /// </summary>
    /// <remarks>
    /// Always use with <c>using</c>. Nothing is written unless <see cref="MarkDirty"/> was called,
    /// so read-only commands open a scope just as freely as mutating ones.
    /// </remarks>
    public sealed class EditTargetScope : IDisposable
    {
        GameObject _prefabRoot;
        PrefabStage _stage;
        bool _ownsPrefabContents;
        bool _dirty;
        bool _disposed;

        public EditTargetKind Kind { get; private set; }

        /// <summary>Prefab asset path for <see cref="EditTargetKind.PrefabAsset"/> / <see cref="EditTargetKind.PrefabStage"/>.</summary>
        public string PrefabPath { get; private set; }

        public Scene Scene { get; private set; }

        /// <summary>True when edits persist straight to a prefab asset on <see cref="Dispose"/>.</summary>
        public bool IsPrefabContent => Kind != EditTargetKind.Scene;

        EditTargetScope()
        {
        }

        /// <summary>
        /// Resolves the edit target. Precedence: explicit <paramref name="prefabPath"/>, then
        /// explicit <paramref name="sceneName"/>, then the open Prefab Mode stage, then the
        /// active scene.
        /// </summary>
        public static EditTargetScope Open(string prefabPath, string sceneName = null)
        {
            if (!string.IsNullOrWhiteSpace(prefabPath))
                return OpenPrefab(AssetPaths.RequirePrefabPath(prefabPath, "prefab"));

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.prefabContentsRoot != null)
                    return FromStage(stage);
            }

            return OpenScene(sceneName);
        }

        static EditTargetScope OpenPrefab(string prefabPath)
        {
            if (!AssetPaths.Exists(prefabPath))
                throw new ArgumentException($"Prefab not found at '{prefabPath}'.");

            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (asset == null)
                throw new ArgumentException($"Asset at '{prefabPath}' is not a prefab.");

            var assetType = PrefabUtility.GetPrefabAssetType(asset);
            if (assetType == PrefabAssetType.Model)
            {
                throw new InvalidOperationException(
                    $"'{prefabPath}' is an imported model prefab and cannot be edited. Create a variant first (prefab_create_variant).");
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null &&
                string.Equals(AssetPaths.Normalize(stage.assetPath), prefabPath, StringComparison.OrdinalIgnoreCase) &&
                stage.prefabContentsRoot != null)
            {
                return FromStage(stage);
            }

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
                throw new InvalidOperationException($"Could not load prefab contents for '{prefabPath}'.");

            return new EditTargetScope
            {
                Kind = EditTargetKind.PrefabAsset,
                PrefabPath = prefabPath,
                Scene = root.scene,
                _prefabRoot = root,
                _ownsPrefabContents = true,
            };
        }

        static EditTargetScope FromStage(PrefabStage stage) =>
            new EditTargetScope
            {
                Kind = EditTargetKind.PrefabStage,
                PrefabPath = AssetPaths.Normalize(stage.assetPath),
                Scene = stage.scene,
                _prefabRoot = stage.prefabContentsRoot,
                _stage = stage,
                _ownsPrefabContents = false,
            };

        static EditTargetScope OpenScene(string sceneName)
        {
            var scene = string.IsNullOrWhiteSpace(sceneName)
                ? SceneManager.GetActiveScene()
                : FindLoadedScene(sceneName);

            if (!scene.IsValid())
            {
                throw new InvalidOperationException(
                    "No valid scene is loaded. Open one with scene_open, or target a prefab asset with the 'prefab' argument.");
            }

            return new EditTargetScope { Kind = EditTargetKind.Scene, Scene = scene };
        }

        static Scene FindLoadedScene(string nameOrPath)
        {
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
                $"Scene '{nameOrPath}' is not loaded. Loaded scenes: {(loaded.Count == 0 ? "(none)" : string.Join(", ", loaded))}.");
        }

        /// <summary>Root GameObjects the node paths are resolved against.</summary>
        public IReadOnlyList<GameObject> Roots()
        {
            if (IsPrefabContent)
                return _prefabRoot == null ? Array.Empty<GameObject>() : new[] { _prefabRoot };

            return Scene.GetRootGameObjects();
        }

        /// <summary>The single prefab content root. Throws for scene targets, which have many roots.</summary>
        public GameObject PrefabRoot =>
            IsPrefabContent
                ? _prefabRoot
                : throw new InvalidOperationException("This edit target is a scene, not a prefab.");

        /// <summary>Marks the target as changed so <see cref="Dispose"/> persists it.</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>
        /// Scenes and the Prefab Mode stage have a live undo stack; contents loaded via
        /// <c>LoadPrefabContents</c> live in a throwaway scene where recording undo is pointless.
        /// </summary>
        /// <remarks>
        /// Play Mode also has to opt out: Unity refuses undo registration there, so
        /// <c>SerializedObject.ApplyModifiedProperties</c> throws "This cannot be used during play
        /// mode" and every write to a live scene object fails. Play-mode edits are discarded when
        /// play stops anyway, so there is nothing an undo entry could be good for.
        /// </remarks>
        public bool SupportsUndo =>
            Kind != EditTargetKind.PrefabAsset && !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>Target identity echoed in every command payload so the caller is never guessing.</summary>
        public Dictionary<string, object> Describe() =>
            new Dictionary<string, object>
            {
                ["kind"] = Kind switch
                {
                    EditTargetKind.PrefabAsset => "prefab_asset",
                    EditTargetKind.PrefabStage => "prefab_stage",
                    _ => "scene",
                },
                ["prefab"] = PrefabPath,
                ["scene"] = IsPrefabContent ? null : (string.IsNullOrEmpty(Scene.path) ? Scene.name : Scene.path),
            };

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            if (!IsPrefabContent)
            {
                // MarkSceneDirty throws "This cannot be used during play mode". Since Dispose runs
                // after the payload is built, that turned a successful write into a reported
                // failure — the worst outcome, because the caller retries an edit that already
                // landed. Play-mode changes are discarded when play stops, so there is nothing to
                // mark dirty anyway.
                if (_dirty && Scene.IsValid() && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorSceneManager.MarkSceneDirty(Scene);
                return;
            }

            try
            {
                if (_dirty && _prefabRoot != null && !string.IsNullOrEmpty(PrefabPath))
                {
                    PrefabUtility.SaveAsPrefabAsset(_prefabRoot, PrefabPath, out var saved);
                    if (!saved)
                        throw new InvalidOperationException($"Failed to save prefab '{PrefabPath}'.");

                    // Saving the asset does not clear the stage's own dirty flag, and a dirty stage
                    // makes Unity raise a modal prompt the next time Prefab Mode is left — which
                    // would block the Editor main thread, and with it the agent's poll loop.
                    _stage?.ClearDirtiness();
                }
            }
            finally
            {
                if (_ownsPrefabContents && _prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(_prefabRoot);
                _prefabRoot = null;
            }
        }
    }
}
