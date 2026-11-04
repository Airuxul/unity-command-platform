using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabEditCommand : EditorActionCommand<PrefabEditParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabEditParams>(
            CommandNames.PrefabEdit,
            CommandHostScope.Editor,
            "Run several node commands (gameobject_*, component_*, prefab instance) on one prefab asset, saved once");

        protected override Dictionary<string, object> Execute(PrefabEditParams p) =>
            EditBatch.RunOps(() => EditTargetScope.Open(p.Prefab), p.Ops, p.DryRun);
    }

    public sealed class SceneEditCommand : EditorActionCommand<SceneEditParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneEditParams>(
            CommandNames.SceneEdit,
            CommandHostScope.Editor,
            "Run several node commands (gameobject_*, component_*, prefab instance) on one loaded scene as one undo step");

        protected override Dictionary<string, object> Execute(SceneEditParams p) =>
            EditBatch.RunOps(() => EditTargetScope.OpenScene(p.Scene), p.Ops, p.DryRun);
    }
}
