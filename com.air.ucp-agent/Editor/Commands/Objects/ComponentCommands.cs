using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class ComponentAddCommand : EditorActionCommand<ComponentAddParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentAddParams>(
            CommandNames.ComponentAdd,
            CommandHostScope.Editor,
            "Add a component to a node, optionally setting properties in the same call");

        protected override Dictionary<string, object> Execute(ComponentAddParams p) => ComponentService.Add(p);
    }

    public sealed class ComponentRemoveCommand : EditorActionCommand<ComponentRemoveParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentRemoveParams>(
            CommandNames.ComponentRemove,
            CommandHostScope.Editor,
            "Remove a component from a node");

        protected override Dictionary<string, object> Execute(ComponentRemoveParams p) =>
            ComponentService.Remove(p);
    }

    public sealed class ComponentListCommand : EditorActionCommand<ComponentListParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentListParams>(
            CommandNames.ComponentList,
            CommandHostScope.Editor,
            "List a node's components with their types, indices and enabled state");

        protected override Dictionary<string, object> Execute(ComponentListParams p) => ComponentService.List(p);
    }

    public sealed class ComponentGetCommand : EditorActionCommand<ComponentGetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentGetParams>(
            CommandNames.ComponentGet,
            CommandHostScope.Editor,
            "Read a component's serialized properties, in the shape component_set accepts back");

        protected override Dictionary<string, object> Execute(ComponentGetParams p) => ComponentService.Get(p);
    }

    public sealed class ComponentSetCommand : EditorActionCommand<ComponentSetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentSetParams>(
            CommandNames.ComponentSet,
            CommandHostScope.Editor,
            "Set component properties; object references accept an asset path, guid, node path or instance id");

        protected override Dictionary<string, object> Execute(ComponentSetParams p) => ComponentService.Set(p);
    }
}
