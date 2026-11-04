using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabInstantiateCommand : EditorActionCommand<PrefabInstantiateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabInstantiateParams>(
            CommandNames.PrefabInstantiate,
            CommandHostScope.Editor,
            "Instantiate a prefab into a scene, or into another prefab asset as a nested prefab");

        protected override Dictionary<string, object> Execute(PrefabInstantiateParams p) =>
            PrefabInstanceService.Instantiate(p);
    }

    public sealed class PrefabApplyCommand : EditorActionCommand<PrefabOverrideParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabOverrideParams>(
            CommandNames.PrefabApply,
            CommandHostScope.Editor,
            "Apply a prefab instance's overrides back to its asset (all, one component, or one property)");

        protected override Dictionary<string, object> Execute(PrefabOverrideParams p) =>
            PrefabInstanceService.Apply(p);
    }

    public sealed class PrefabRevertCommand : EditorActionCommand<PrefabOverrideParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabOverrideParams>(
            CommandNames.PrefabRevert,
            CommandHostScope.Editor,
            "Discard a prefab instance's overrides (all, one component, or one property)");

        protected override Dictionary<string, object> Execute(PrefabOverrideParams p) =>
            PrefabInstanceService.Revert(p);
    }

    public sealed class PrefabOverridesCommand : EditorActionCommand<PrefabOverridesParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabOverridesParams>(
            CommandNames.PrefabOverrides,
            CommandHostScope.Editor,
            "List a prefab instance's overrides: modified properties, added/removed components, added GameObjects");

        protected override Dictionary<string, object> Execute(PrefabOverridesParams p) =>
            PrefabInstanceService.Overrides(p);
    }

    public sealed class PrefabUnpackCommand : EditorActionCommand<PrefabUnpackParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabUnpackParams>(
            CommandNames.PrefabUnpack,
            CommandHostScope.Editor,
            "Break a prefab instance's link to its asset, leaving plain GameObjects");

        protected override Dictionary<string, object> Execute(PrefabUnpackParams p) =>
            PrefabInstanceService.Unpack(p);
    }

    public sealed class PrefabReplaceCommand : EditorActionCommand<PrefabReplaceParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabReplaceParams>(
            CommandNames.PrefabReplace,
            CommandHostScope.Editor,
            "Swap a prefab instance's source asset, keeping its place in the hierarchy (overrides are lost)");

        protected override Dictionary<string, object> Execute(PrefabReplaceParams p) =>
            PrefabInstanceService.Replace(p);
    }

    public sealed class PrefabInstanceInfoCommand : EditorActionCommand<PrefabInstanceParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabInstanceParams>(
            CommandNames.PrefabInstanceInfo,
            CommandHostScope.Editor,
            "Prefab linkage of a node: source asset, instance roots, connection status, override counts");

        protected override Dictionary<string, object> Execute(PrefabInstanceParams p) =>
            PrefabInstanceService.Info(p);
    }
}
