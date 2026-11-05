using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabInstantiateCommand : EditTargetCommand<PrefabInstantiateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabInstantiateParams>(
            CommandNames.PrefabInstantiate,
            CommandHostScope.Editor,
            "Instantiate a prefab into a scene, or into another prefab asset as a nested prefab");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabInstantiateParams p) =>
            PrefabInstanceService.Instantiate(scope, p);
    }

    public sealed class PrefabApplyCommand : EditTargetCommand<PrefabOverrideParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabOverrideParams>(
            CommandNames.PrefabApply,
            CommandHostScope.Editor,
            "Apply a prefab instance's overrides back to its asset (all, one component, or one property)");

        // Writes the source prefab asset at once; a batch could not take that back.
        public override bool Batchable => false;

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabOverrideParams p) =>
            PrefabInstanceService.Apply(scope, p);
    }

    public sealed class PrefabRevertCommand : EditTargetCommand<PrefabOverrideParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabOverrideParams>(
            CommandNames.PrefabRevert,
            CommandHostScope.Editor,
            "Discard a prefab instance's overrides (all, one component, or one property)");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabOverrideParams p) =>
            PrefabInstanceService.Revert(scope, p);
    }

    public sealed class PrefabOverridesCommand : EditTargetCommand<PrefabOverridesParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabOverridesParams>(
            CommandNames.PrefabOverrides,
            CommandHostScope.Editor,
            "List a prefab instance's overrides: modified properties, added/removed components, added GameObjects");

        protected override bool ReadOnly => true;

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabOverridesParams p) =>
            PrefabInstanceService.Overrides(scope, p);
    }

    public sealed class PrefabUnpackCommand : EditTargetCommand<PrefabUnpackParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabUnpackParams>(
            CommandNames.PrefabUnpack,
            CommandHostScope.Editor,
            "Break a prefab instance's link to its asset, leaving plain GameObjects");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabUnpackParams p) =>
            PrefabInstanceService.Unpack(scope, p);
    }

    public sealed class PrefabReplaceCommand : EditTargetCommand<PrefabReplaceParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabReplaceParams>(
            CommandNames.PrefabReplace,
            CommandHostScope.Editor,
            "Swap a prefab instance's source asset, keeping its place in the hierarchy (overrides are lost)");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabReplaceParams p) =>
            PrefabInstanceService.Replace(scope, p);
    }

    public sealed class PrefabInstanceInfoCommand : EditTargetCommand<PrefabInstanceParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabInstanceParams>(
            CommandNames.PrefabInstanceInfo,
            CommandHostScope.Editor,
            "Prefab linkage of a node: source asset, instance roots, connection status, override counts");

        protected override bool ReadOnly => true;

        protected override Dictionary<string, object> Apply(EditTargetScope scope, PrefabInstanceParams p) =>
            PrefabInstanceService.Info(scope, p);
    }
}
