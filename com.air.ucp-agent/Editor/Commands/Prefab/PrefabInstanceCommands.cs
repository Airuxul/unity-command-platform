using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class PrefabInstantiateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabInstantiateParams>(
            CommandNames.PrefabInstantiate,
            CommandHostScope.Editor,
            "Instantiate a prefab into a scene, or into another prefab asset as a nested prefab");

        public static object Execute(PrefabInstantiateParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Instantiate(ctx.Scope, p);
    }

    [UcpCommand]
    public static class PrefabApplyCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabOverrideParams>(
            CommandNames.PrefabApply,
            CommandHostScope.Editor,
            "Apply a prefab instance's overrides back to its asset (all, one component, or one property)");

        public static object Execute(PrefabOverrideParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Apply(ctx.Scope, p);
    }

    [UcpCommand]
    public static class PrefabRevertCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabOverrideParams>(
            CommandNames.PrefabRevert,
            CommandHostScope.Editor,
            "Discard a prefab instance's overrides (all, one component, or one property)");

        public static object Execute(PrefabOverrideParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Revert(ctx.Scope, p);
    }

    [UcpCommand]
    public static class PrefabOverridesCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabOverridesParams>(
            CommandNames.PrefabOverrides,
            CommandHostScope.Editor,
            "List a prefab instance's overrides: modified properties, added/removed components, added GameObjects")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(PrefabOverridesParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Overrides(ctx.Scope, p);
    }

    [UcpCommand]
    public static class PrefabUnpackCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabUnpackParams>(
            CommandNames.PrefabUnpack,
            CommandHostScope.Editor,
            "Break a prefab instance's link to its asset, leaving plain GameObjects");

        public static object Execute(PrefabUnpackParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Unpack(ctx.Scope, p);
    }

    [UcpCommand]
    public static class PrefabReplaceCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabReplaceParams>(
            CommandNames.PrefabReplace,
            CommandHostScope.Editor,
            "Swap a prefab instance's source asset, keeping its place in the hierarchy (overrides are lost)");

        public static object Execute(PrefabReplaceParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Replace(ctx.Scope, p);
    }

    [UcpCommand]
    public static class PrefabInstanceInfoCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabInstanceParams>(
            CommandNames.PrefabInstanceInfo,
            CommandHostScope.Editor,
            "Prefab linkage of a node: source asset, instance roots, connection status, override counts")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(PrefabInstanceParams p, EditTargetContext ctx) =>
            PrefabInstanceService.Info(ctx.Scope, p);
    }
}
