using System;
using System.Collections.Generic;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    /// <summary>The untyped face of <see cref="EditTargetCommand{TParams}"/> that a batch runs.</summary>
    public interface IEditTargetCommand
    {
        string Name { get; }
        IReadOnlyList<string> Aliases { get; }
        Type ParamsType { get; }
        bool Batchable { get; }
        Dictionary<string, object> Apply(EditTargetScope scope, object args);
    }

    /// <summary>
    /// A command that works inside one edit target (a scene or a prefab). It only implements
    /// <see cref="Apply"/>; opening, committing and reverting the target is <see cref="EditBatch"/>'s
    /// job, which is what lets the same command run alone or as an op of prefab_edit / scene_edit.
    /// Every result carries the resolved <c>target</c>, added here so <see cref="Apply"/> never has to.
    /// </summary>
    public abstract class EditTargetCommand<TParams> : EditorActionCommand<TParams>, IEditTargetCommand
        where TParams : EditTargetParams
    {
        /// <summary>
        /// False for commands that also write outside the target (prefab_apply writes the source
        /// asset at once), which a batch could not take back if a later op failed.
        /// </summary>
        public virtual bool Batchable => true;

        /// <summary>True for commands that never write: run alone, they skip the undo group.</summary>
        protected virtual bool ReadOnly => false;

        protected abstract Dictionary<string, object> Apply(EditTargetScope scope, TParams p);

        protected sealed override Dictionary<string, object> Execute(TParams p)
        {
            if (!ReadOnly)
                return EditBatch.Run(EditTargetScope.Open(p.Prefab, p.Scene), scope => ApplyWithTarget(scope, p));

            using var peek = EditTargetScope.Peek(p.Prefab, p.Scene);
            return ApplyWithTarget(peek, p);
        }

        Dictionary<string, object> ApplyWithTarget(EditTargetScope scope, TParams p)
        {
            var result = Apply(scope, p);
            result["target"] = scope.Describe();
            return result;
        }

        string IEditTargetCommand.Name => Descriptor.Name;
        IReadOnlyList<string> IEditTargetCommand.Aliases => Descriptor.Aliases ?? Array.Empty<string>();
        Type IEditTargetCommand.ParamsType => typeof(TParams);

        Dictionary<string, object> IEditTargetCommand.Apply(EditTargetScope scope, object args) =>
            ApplyWithTarget(scope, (TParams)args);
    }
}
