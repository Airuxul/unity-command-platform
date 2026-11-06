using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class ComponentAddCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ComponentAddParams>(
            CommandNames.ComponentAdd,
            CommandHostScope.Editor,
            "Add a component to a node, optionally setting properties in the same call");

        public static object Execute(ComponentAddParams p, EditTargetContext ctx) => ComponentService.Add(ctx.Scope, p);
    }

    [UcpCommand]
    public static class ComponentRemoveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ComponentRemoveParams>(
            CommandNames.ComponentRemove,
            CommandHostScope.Editor,
            "Remove a component from a node");

        public static object Execute(ComponentRemoveParams p, EditTargetContext ctx) =>
            ComponentService.Remove(ctx.Scope, p);
    }

    [UcpCommand]
    public static class ComponentListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ComponentListParams>(
            CommandNames.ComponentList,
            CommandHostScope.Editor,
            "List a node's components with their types, indices and enabled state")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(ComponentListParams p, EditTargetContext ctx) => ComponentService.List(ctx.Scope, p);
    }

    [UcpCommand]
    public static class ComponentGetCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ComponentGetParams>(
            CommandNames.ComponentGet,
            CommandHostScope.Editor,
            $"Read a component's serialized properties, in the shape {CommandNames.ComponentSet} accepts back")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(ComponentGetParams p, EditTargetContext ctx) => ComponentService.Get(ctx.Scope, p);
    }

    [UcpCommand]
    public static class ComponentSetCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ComponentSetParams>(
            CommandNames.ComponentSet,
            CommandHostScope.Editor,
            "Set component properties; object references accept an asset path, guid, node path or instance id");

        public static object Execute(ComponentSetParams p, EditTargetContext ctx) => ComponentService.Set(ctx.Scope, p);
    }
}
