using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class ComponentAddCommand : EditTargetCommand<ComponentAddParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentAddParams>(
            CommandNames.ComponentAdd,
            CommandHostScope.Editor,
            "Add a component to a node, optionally setting properties in the same call");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, ComponentAddParams p) => ComponentService.Add(scope, p);
    }

    public sealed class ComponentRemoveCommand : EditTargetCommand<ComponentRemoveParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentRemoveParams>(
            CommandNames.ComponentRemove,
            CommandHostScope.Editor,
            "Remove a component from a node");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, ComponentRemoveParams p) =>
            ComponentService.Remove(scope, p);
    }

    public sealed class ComponentListCommand : EditTargetCommand<ComponentListParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentListParams>(
            CommandNames.ComponentList,
            CommandHostScope.Editor,
            "List a node's components with their types, indices and enabled state");

        protected override bool ReadOnly => true;

        protected override Dictionary<string, object> Apply(EditTargetScope scope, ComponentListParams p) => ComponentService.List(scope, p);
    }

    public sealed class ComponentGetCommand : EditTargetCommand<ComponentGetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentGetParams>(
            CommandNames.ComponentGet,
            CommandHostScope.Editor,
            $"Read a component's serialized properties, in the shape {CommandNames.ComponentSet} accepts back");

        protected override bool ReadOnly => true;

        protected override Dictionary<string, object> Apply(EditTargetScope scope, ComponentGetParams p) => ComponentService.Get(scope, p);
    }

    public sealed class ComponentSetCommand : EditTargetCommand<ComponentSetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ComponentSetParams>(
            CommandNames.ComponentSet,
            CommandHostScope.Editor,
            "Set component properties; object references accept an asset path, guid, node path or instance id");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, ComponentSetParams p) => ComponentService.Set(scope, p);
    }
}
