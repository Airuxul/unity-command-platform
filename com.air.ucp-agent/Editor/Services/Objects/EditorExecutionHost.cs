using System;
using System.Collections.Generic;
using System.IO;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// The Editor's <see cref="IExecutionHost"/>: the one place an edit target is written. It opens
    /// the target, runs the command, then saves on success or takes the command back on failure,
    /// so a failed command never leaves half an edit.
    /// </summary>
    /// <remarks>
    /// Taking a step back is the step's Unity undo group, then the command's own <c>Undo</c> for
    /// what Unity does not record (a new asset file). A prefab file a group already saved is
    /// written back from the copy taken when the group first opened it.
    /// </remarks>
    public sealed class EditorExecutionHost : IExecutionHost
    {
        public object Run(CommandEntry entry, object parameters, CommandContext ctx)
        {
            if (!entry.EditsTarget)
                return RunPlain(entry, parameters, ctx);

            var target = (EditTargetParams)parameters;
            var edit = (EditTargetContext)ctx;
            if (entry.Descriptor.ReadsTargetOnly)
            {
                using var peek = EditTargetScope.Peek(target.Prefab, target.Scene);
                edit.Scope = peek;
                edit.TargetKind = peek.KindName;
                edit.Precondition?.Invoke(peek);
                return WithTarget(entry.Execute(parameters, ctx), peek);
            }

            var group = ctx.Group;
            var shared = group is { SharesTargets: true };
            var scope = shared ? SharedTargets.Acquire(group, target) : EditTargetScope.Open(target.Prefab, target.Scene);
            edit.Scope = scope;
            edit.TargetKind = scope.KindName;
            edit.UndoGroup = scope.BeginStep();
            if (group != null && scope.IsPrefabContent)
                BackUpPrefab(group, scope, edit);

            try
            {
                edit.Precondition?.Invoke(scope);
                var result = entry.Execute(parameters, ctx);
                if (ctx.HasFailed)
                {
                    TakeBack(entry, parameters, edit, shared);
                    if (!shared)
                        scope.Dispose();
                    return result;
                }

                var payload = WithTarget(result, scope);
                if (!shared)
                {
                    scope.Commit();
                    scope.Dispose();
                }

                return payload;
            }
            catch (Exception ex)
            {
                Exception error = ex;
                try
                {
                    TakeBack(entry, parameters, edit, shared);
                }
                catch (Exception undoError)
                {
                    // The caller needs the reason the edit failed, not only the rollback's.
                    error = UcpException.Wrap(
                        ex,
                        $"{ex.Message} Rolling the edit back also failed, so the target may be half-edited: {undoError.Message}");
                }
                finally
                {
                    if (!shared)
                        scope.Dispose();
                }

                if (error == ex)
                    throw;
                throw error;
            }
        }

        public void Undo(CommandEntry entry, object parameters, CommandContext ctx)
        {
            if (ctx is not EditTargetContext edit)
            {
                entry.Undo(parameters, ctx);
                return;
            }

            if (edit.UndoGroup >= 0)
            {
                UnityEditor.Undo.RevertAllDownToGroup(edit.UndoGroup);
            }
            else if (edit.TargetKind != EditTargetScope.PrefabAssetKind && string.IsNullOrEmpty(edit.PrefabBackup))
            {
                throw new UcpException(
                    UcpErrorCodes.EditModeRequired,
                    $"'{entry.Name}' ran in Play Mode, which records no undo; its change stays until play stops.");
            }

            entry.Undo(parameters, ctx);
            RestorePrefab(edit);
        }

        static object RunPlain(CommandEntry entry, object parameters, CommandContext ctx)
        {
            object result;
            try
            {
                result = entry.Execute(parameters, ctx);
            }
            catch (Exception ex)
            {
                var error = ExecutionHost.UndoAfterFailure(entry, parameters, ctx, ex);
                if (error == ex)
                    throw;
                throw error;
            }

            if (ctx.HasFailed && entry.CanUndo)
                entry.Undo(parameters, ctx);
            return result;
        }

        /// <summary>
        /// One failed step: its own undo group when the target is shared with the steps before it,
        /// the whole target otherwise, then the command's own Undo.
        /// </summary>
        static void TakeBack(CommandEntry entry, object parameters, EditTargetContext edit, bool shared)
        {
            Exception error = null;
            try
            {
                if (shared)
                    edit.Scope.RevertStep(edit.UndoGroup);
                else
                    edit.Scope.Revert();
            }
            catch (Exception ex)
            {
                error = ex;
            }

            try
            {
                entry.Undo(parameters, edit);
            }
            catch (Exception ex)
            {
                error ??= ex;
            }

            if (error != null)
                throw error;
        }

        static object WithTarget(object result, EditTargetScope scope)
        {
            if (result is Dictionary<string, object> dict)
            {
                dict["target"] = scope.Describe();
                return dict;
            }

            return result ?? new Dictionary<string, object> { ["target"] = scope.Describe() };
        }

        /// <summary>
        /// Copies the prefab file the first time a group opens it, so the group can put it back
        /// after it was saved. Later steps on the same open target need no copy of their own.
        /// </summary>
        static void BackUpPrefab(CommandGroupInfo group, EditTargetScope scope, EditTargetContext edit)
        {
            edit.PrefabPath = scope.PrefabPath;
            if (scope.BackedUp)
                return;

            var source = AssetPaths.ToFullPath(scope.PrefabPath);
            var backup = Path.Combine(group.Directory, "prefab-" + Guid.NewGuid().ToString("N") + ".prefab");
            Directory.CreateDirectory(group.Directory);
            File.Copy(source, backup, overwrite: true);
            edit.PrefabBackup = backup;
            scope.BackedUp = true;
        }

        static void RestorePrefab(EditTargetContext edit)
        {
            if (string.IsNullOrEmpty(edit.PrefabBackup) || string.IsNullOrEmpty(edit.PrefabPath))
                return;
            if (!File.Exists(edit.PrefabBackup))
                throw new IOException($"The copy of '{edit.PrefabPath}' taken before the group is gone ({edit.PrefabBackup}).");

            var path = AssetPaths.ToFullPath(edit.PrefabPath);
            var before = File.ReadAllBytes(edit.PrefabBackup);
            if (File.Exists(path) && AreEqual(File.ReadAllBytes(path), before))
                return;

            File.WriteAllBytes(path, before);
            AssetDatabase.ImportAsset(edit.PrefabPath, ImportAssetOptions.ForceSynchronousImport);

            // The stage was reverted by its undo group; the file now matches it again.
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && string.Equals(AssetPaths.Normalize(stage.assetPath), edit.PrefabPath, StringComparison.OrdinalIgnoreCase))
                stage.ClearDirtiness();
        }

        static bool AreEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }
    }
}
