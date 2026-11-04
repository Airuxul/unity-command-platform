using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    // Every command here takes an optional `prefab` argument. With it they edit that prefab asset
    // in place; without it they edit the open scene. One implementation, both scopes.

    public sealed class GameObjectCreateCommand : EditTargetCommand<GameObjectCreateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectCreateParams>(
            CommandNames.GameObjectCreate,
            CommandHostScope.Editor,
            "Create a GameObject in a scene or inside a prefab asset");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectCreateParams p) =>
            GameObjectService.Create(scope, p);
    }

    public sealed class GameObjectDeleteCommand : EditTargetCommand<NodeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<NodeParams>(
            CommandNames.GameObjectDelete,
            CommandHostScope.Editor,
            "Delete a GameObject and its children");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, NodeParams p) => GameObjectService.Delete(scope, p);
    }

    public sealed class GameObjectDuplicateCommand : EditTargetCommand<GameObjectDuplicateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectDuplicateParams>(
            CommandNames.GameObjectDuplicate,
            CommandHostScope.Editor,
            "Duplicate a GameObject, optionally under a different parent");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectDuplicateParams p) =>
            GameObjectService.Duplicate(scope, p);
    }

    public sealed class GameObjectRenameCommand : EditTargetCommand<GameObjectRenameParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectRenameParams>(
            CommandNames.GameObjectRename,
            CommandHostScope.Editor,
            "Rename a GameObject");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectRenameParams p) =>
            GameObjectService.Rename(scope, p);
    }

    public sealed class GameObjectReparentCommand : EditTargetCommand<GameObjectReparentParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectReparentParams>(
            CommandNames.GameObjectReparent,
            CommandHostScope.Editor,
            "Move a GameObject under a new parent and/or sibling index");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectReparentParams p) =>
            GameObjectService.Reparent(scope, p);
    }

    public sealed class GameObjectTransformCommand : EditTargetCommand<GameObjectTransformParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectTransformParams>(
            CommandNames.GameObjectTransform,
            CommandHostScope.Editor,
            "Set position, rotation and/or scale, in local or world space, absolute or relative");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectTransformParams p) =>
            GameObjectService.SetTransform(scope, p);
    }

    public sealed class GameObjectActivateCommand : EditTargetCommand<GameObjectActivateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectActivateParams>(
            CommandNames.GameObjectActivate,
            CommandHostScope.Editor,
            "Enable or disable a GameObject (omit 'active' to toggle)");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectActivateParams p) =>
            GameObjectService.Activate(scope, p);
    }

    public sealed class GameObjectTagCommand : EditTargetCommand<GameObjectTagParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectTagParams>(
            CommandNames.GameObjectTag,
            CommandHostScope.Editor,
            "Assign a tag to a GameObject, optionally to its whole subtree");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectTagParams p) =>
            GameObjectService.SetTag(scope, p);
    }

    public sealed class GameObjectLayerCommand : EditTargetCommand<GameObjectLayerParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectLayerParams>(
            CommandNames.GameObjectLayer,
            CommandHostScope.Editor,
            "Assign a layer to a GameObject, optionally to its whole subtree");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectLayerParams p) =>
            GameObjectService.SetLayer(scope, p);
    }

    public sealed class GameObjectInfoCommand : EditTargetCommand<NodeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<NodeParams>(
            CommandNames.GameObjectInfo,
            CommandHostScope.Editor,
            "Everything about one GameObject: transform, tag, layer, components, children, prefab linkage");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, NodeParams p) => GameObjectService.Info(scope, p);
    }

    public sealed class GameObjectTreeCommand : EditTargetCommand<GameObjectTreeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectTreeParams>(
            CommandNames.GameObjectTree,
            CommandHostScope.Editor,
            "Hierarchy dump from a node or from the scene roots");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectTreeParams p) =>
            GameObjectService.Tree(scope, p);
    }

    public sealed class GameObjectFindCommand : EditTargetCommand<GameObjectFindParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectFindParams>(
            CommandNames.GameObjectFind,
            CommandHostScope.Editor,
            "Search GameObjects by name, tag, layer, component or path");

        protected override Dictionary<string, object> Apply(EditTargetScope scope, GameObjectFindParams p) =>
            GameObjectService.Find(scope, p);
    }
}
