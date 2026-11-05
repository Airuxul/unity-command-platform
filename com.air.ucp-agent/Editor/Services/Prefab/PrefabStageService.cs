using System;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services.Prefab
{
    /// <summary>
    /// Prefab Mode (the isolated prefab editing stage). Editing a prefab does <em>not</em> require
    /// this — <c>gameobject_*</c> / <c>component_*</c> with the <c>prefab</c> argument edit the asset
    /// directly. Use these commands when the prefab should also be visible in the Editor UI.
    /// </summary>
    /// <remarks>
    /// Leaving Prefab Mode with unsaved changes makes Unity raise a modal "save changes?" dialog,
    /// which blocks the Editor main thread — and that thread is also the agent's poll loop, so the
    /// whole agent would hang until a human clicked the dialog. Every path here therefore either
    /// saves or explicitly clears the stage's dirty flag before leaving, and refuses rather than
    /// letting Unity ask.
    /// </remarks>
    public static class PrefabStageService
    {
        public static Dictionary<string, object> Open(PrefabStageOpenParams p)
        {
            var path = AssetPaths.RequireExisting(p.Path, ParamKeys.Path);
            AssetPaths.RequireAsset<GameObject>(path, ParamKeys.Path);

            var current = PrefabStageUtility.GetCurrentPrefabStage();
            if (current != null && current.scene.isDirty)
            {
                var openPath = AssetPaths.Normalize(current.assetPath);
                if (!p.Force)
                {
                    throw new InvalidOperationException(
                        $"'{openPath}' is already open in Prefab Mode with unsaved changes. " +
                        $"Run {CommandNames.PrefabSave}, or pass force=true to discard them.");
                }

                current.ClearDirtiness();
            }

            GameObject context = null;
            if (!string.IsNullOrWhiteSpace(p.Node))
            {
                // The context instance lives in the scene; Open(null, null) would search the stage
                // that is about to be replaced when Prefab Mode is already open.
                using var scope = EditTargetScope.PeekScene(null);
                context = GameObjectPath.Require(scope, p.Node, ParamKeys.Node).gameObject;
            }

            var opened = context == null
                ? PrefabStageUtility.OpenPrefab(path)
                : PrefabStageUtility.OpenPrefab(path, context);

            if (!opened)
                throw new InvalidOperationException($"Unity refused to open '{path}' in Prefab Mode.");

            return Describe("opened");
        }

        public static Dictionary<string, object> Save()
        {
            var stage = RequireStage();
            SaveStage(stage);
            return Describe("saved");
        }

        public static Dictionary<string, object> Close(PrefabCloseParams p)
        {
            var stage = RequireStage();
            var path = AssetPaths.Normalize(stage.assetPath);
            var save = p?.Save ?? true;
            var wasDirty = stage.scene.isDirty;

            if (save)
            {
                SaveStage(stage);
            }
            else if (wasDirty)
            {
                if (!(p?.Force ?? false))
                {
                    throw new InvalidOperationException(
                        $"'{path}' has unsaved changes. Call {CommandNames.PrefabClose} with save=true, or force=true to discard them.");
                }

                // Clearing the flag is what keeps GoToMainStage from opening a modal prompt.
                stage.ClearDirtiness();
            }

            StageUtility.GoToMainStage();

            return new Dictionary<string, object>
            {
                ["closed"] = true,
                ["path"] = path,
                ["saved"] = save,
                ["had_unsaved_changes"] = wasDirty,
                ["discarded_changes"] = !save && wasDirty,
            };
        }

        public static Dictionary<string, object> Info()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
            {
                return new Dictionary<string, object>
                {
                    ["open"] = false,
                    ["path"] = null,
                };
            }

            return Describe("open");
        }

        /// <summary>
        /// Writes the open stage back to its asset and clears the stage's dirty flag.
        /// <see cref="PrefabStage"/> has no public <c>Save()</c>, and saving the asset alone leaves
        /// the stage marked dirty — which is what would later trigger the modal.
        /// </summary>
        public static void SaveStage(PrefabStage stage)
        {
            var path = AssetPaths.Normalize(stage.assetPath);
            PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path, out var saved);
            if (!saved)
                throw new InvalidOperationException($"Unity could not save '{path}'.");

            stage.ClearDirtiness();
        }

        /// <summary>
        /// Refuses to delete or move an asset that is open in Prefab Mode. Pulling the asset out
        /// from under a live stage is another path that ends in a modal or a broken stage.
        /// </summary>
        public static void RequireNotOpenInStage(string assetOrFolderPath, string action)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                return;

            var open = AssetPaths.Normalize(stage.assetPath);
            var target = AssetPaths.Normalize(assetOrFolderPath);
            if (string.IsNullOrEmpty(open) || string.IsNullOrEmpty(target))
                return;

            var hit = string.Equals(open, target, StringComparison.OrdinalIgnoreCase) ||
                      open.StartsWith(target + "/", StringComparison.OrdinalIgnoreCase);
            if (!hit)
                return;

            throw new InvalidOperationException(
                $"'{open}' is open in Prefab Mode and cannot be {action}. Run {CommandNames.PrefabClose} first.");
        }

        static Dictionary<string, object> Describe(string state)
        {
            var stage = RequireStage();
            var root = stage.prefabContentsRoot;

            return new Dictionary<string, object>
            {
                ["open"] = true,
                ["state"] = state,
                ["path"] = AssetPaths.Normalize(stage.assetPath),
                ["root"] = root == null ? null : root.name,
                ["dirty"] = stage.scene.isDirty,
                ["mode"] = stage.mode.ToString(),
                ["child_count"] = root == null ? 0 : root.transform.childCount,
                ["components"] = root == null ? null : ComponentService.Names(root),
            };
        }

        static PrefabStage RequireStage()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null)
                throw new InvalidOperationException($"No prefab is open in Prefab Mode. Open one with {CommandNames.PrefabOpen}.");

            if (stage.prefabContentsRoot == null)
                throw new InvalidOperationException("The open prefab stage has no contents root.");

            return stage;
        }
    }
}
