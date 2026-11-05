using System;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using Air.UcpAgent.Editor.Services.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

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
    /// Always use with <c>using</c>. <see cref="Dispose"/> only releases what was loaded; writes are
    /// persisted by <see cref="Commit"/> and undone by <see cref="Revert"/>, which
    /// <see cref="EditBatch.Run{T}"/> calls. Read-only callers just open and dispose.
    /// </remarks>
    public sealed class EditTargetScope : IDisposable
    {
        GameObject _prefabRoot;
        PrefabStage _stage;
        bool _ownsPrefabContents;
        bool _dirty;
        bool _disposed;
        int _undoGroup = -1;
        bool _stageHadUnsavedChanges;

        public EditTargetKind Kind { get; private set; }

        /// <summary>Prefab asset path for <see cref="EditTargetKind.PrefabAsset"/> / <see cref="EditTargetKind.PrefabStage"/>.</summary>
        public string PrefabPath { get; private set; }

        public Scene Scene { get; private set; }

        /// <summary>True when <see cref="Commit"/> writes a prefab asset rather than marking a scene dirty.</summary>
        public bool IsPrefabContent => Kind != EditTargetKind.Scene;

        EditTargetScope()
        {
        }

        /// <summary>
        /// Resolves the edit target. Precedence: explicit <paramref name="prefabPath"/>, then
        /// explicit <paramref name="sceneName"/>, then the open Prefab Mode stage, then the
        /// active scene.
        /// </summary>
        public static EditTargetScope Open(string prefabPath, string sceneName = null) =>
            BeginUndoGroup(Resolve(prefabPath, sceneName));

        /// <summary>A loaded scene only — never the Prefab Mode stage, whatever is open.</summary>
        public static EditTargetScope OpenScene(string sceneName) => BeginUndoGroup(LoadedScene(sceneName));

        /// <summary>
        /// The same target as <see cref="Open"/> for a caller that only reads: no undo group is
        /// opened, so there is nothing to commit or revert.
        /// </summary>
        public static EditTargetScope Peek(string prefabPath, string sceneName = null) => Resolve(prefabPath, sceneName);

        /// <summary>Read-only <see cref="OpenScene"/>.</summary>
        public static EditTargetScope PeekScene(string sceneName) => LoadedScene(sceneName);

        static EditTargetScope BeginUndoGroup(EditTargetScope scope)
        {
            if (scope.SupportsUndo)
            {
                // Everything recorded from here on belongs to this scope: Revert undoes exactly that,
                // Commit folds it into one undo step.
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("ucp edit");
                scope._undoGroup = Undo.GetCurrentGroup();
            }

            return scope;
        }

        static EditTargetScope Resolve(string prefabPath, string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(prefabPath))
                return OpenPrefab(AssetPaths.RequirePrefabPath(prefabPath, ParamKeys.Prefab));

            if (string.IsNullOrWhiteSpace(sceneName))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null && stage.prefabContentsRoot != null)
                    return FromStage(stage);
            }

            return LoadedScene(sceneName);
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
                    $"'{prefabPath}' is an imported model prefab and cannot be edited. Create a variant first ({CommandNames.PrefabCreateVariant}).");
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
                _stageHadUnsavedChanges = stage.scene.isDirty,
            };

        static EditTargetScope LoadedScene(string sceneName) =>
            new EditTargetScope { Kind = EditTargetKind.Scene, Scene = SceneLookup.Require(sceneName) };

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

        /// <summary>Marks the target as changed so <see cref="Commit"/> persists it.</summary>
        public void MarkDirty() => _dirty = true;

        // Undo shims: recording undo is required in a scene or stage and refused for off-screen
        // prefab contents and in Play Mode (see SupportsUndo).

        public void RecordUndo(Object target, string label)
        {
            if (SupportsUndo && target != null)
                Undo.RecordObject(target, label);
        }

        public void RegisterCreated(GameObject created, string label)
        {
            if (SupportsUndo && created != null)
                Undo.RegisterCreatedObjectUndo(created, label);
        }

        /// <summary>Destroys a GameObject or component so that <see cref="Revert"/> can bring it back.</summary>
        public void Destroy(Object target)
        {
            if (target == null)
                return;

            if (SupportsUndo)
                Undo.DestroyObjectImmediate(target);
            else
                Object.DestroyImmediate(target);
        }

        /// <summary>
        /// Whether <see cref="Revert"/> really takes changes back: off-screen prefab contents are
        /// just not saved, the rest needs an undo group, which Play Mode does not record.
        /// </summary>
        public bool CanRevert => Kind == EditTargetKind.PrefabAsset || _undoGroup >= 0;

        /// <summary>
        /// Persists the changes: a prefab target is saved to its asset, a scene is marked dirty
        /// (writing the scene file is scene_save's job). No-op when nothing was marked dirty.
        /// </summary>
        public void Commit()
        {
            if (!_dirty)
                return;

            // Saving the stage writes everything in it, including the user's own pending edits.
            if (_stageHadUnsavedChanges)
            {
                throw new InvalidOperationException(
                    $"'{PrefabPath}' is open in Prefab Mode with unsaved changes. Save or discard them first ({CommandNames.PrefabSave} / {CommandNames.PrefabClose}).");
            }

            if (_undoGroup >= 0)
                Undo.CollapseUndoOperations(_undoGroup);

            if (!IsPrefabContent)
            {
                // MarkSceneDirty throws "This cannot be used during play mode", and play-mode
                // changes are discarded when play stops anyway.
                if (Scene.IsValid() && !EditorApplication.isPlayingOrWillChangePlaymode)
                    EditorSceneManager.MarkSceneDirty(Scene);
                return;
            }

            PrefabUtility.SaveAsPrefabAsset(_prefabRoot, PrefabPath, out var saved);
            if (!saved)
                throw new InvalidOperationException($"Failed to save prefab '{PrefabPath}'.");

            // Saving the asset does not clear the stage's own dirty flag, and a dirty stage makes
            // Unity raise a modal prompt the next time Prefab Mode is left — which would block the
            // Editor main thread, and with it the agent's poll loop.
            _stage?.ClearDirtiness();
        }

        /// <summary>
        /// Drops the changes. Off-screen prefab contents need nothing (they are discarded on
        /// <see cref="Dispose"/>); scenes and the Prefab Mode stage roll back their undo group.
        /// Play Mode records no undo, so a failed play-mode edit stays until play stops.
        /// </summary>
        public void Revert()
        {
            if (_undoGroup >= 0)
                Undo.RevertAllDownToGroup(_undoGroup);
        }

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

            if (_ownsPrefabContents && _prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(_prefabRoot);
            _prefabRoot = null;
        }
    }
}
