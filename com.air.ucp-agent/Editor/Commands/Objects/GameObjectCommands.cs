using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    // Every command here takes an optional `prefab` argument. With it they edit that prefab asset
    // in place; without it they edit the open scene. One implementation, both scopes.

    public sealed class GameObjectCreateCommand : EditorActionCommand<GameObjectCreateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectCreateParams>(
            CommandNames.GameObjectCreate,
            CommandHostScope.Editor,
            "Create a GameObject in a scene or inside a prefab asset");

        protected override Dictionary<string, object> Execute(GameObjectCreateParams p) =>
            GameObjectService.Create(p);
    }

    public sealed class GameObjectDeleteCommand : EditorActionCommand<NodeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<NodeParams>(
            CommandNames.GameObjectDelete,
            CommandHostScope.Editor,
            "Delete a GameObject and its children");

        protected override Dictionary<string, object> Execute(NodeParams p) => GameObjectService.Delete(p);
    }

    public sealed class GameObjectDuplicateCommand : EditorActionCommand<GameObjectDuplicateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectDuplicateParams>(
            CommandNames.GameObjectDuplicate,
            CommandHostScope.Editor,
            "Duplicate a GameObject, optionally under a different parent");

        protected override Dictionary<string, object> Execute(GameObjectDuplicateParams p) =>
            GameObjectService.Duplicate(p);
    }

    public sealed class GameObjectRenameCommand : EditorActionCommand<GameObjectRenameParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectRenameParams>(
            CommandNames.GameObjectRename,
            CommandHostScope.Editor,
            "Rename a GameObject");

        protected override Dictionary<string, object> Execute(GameObjectRenameParams p) =>
            GameObjectService.Rename(p);
    }

    public sealed class GameObjectReparentCommand : EditorActionCommand<GameObjectReparentParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectReparentParams>(
            CommandNames.GameObjectReparent,
            CommandHostScope.Editor,
            "Move a GameObject under a new parent and/or sibling index");

        protected override Dictionary<string, object> Execute(GameObjectReparentParams p) =>
            GameObjectService.Reparent(p);
    }

    public sealed class GameObjectTransformCommand : EditorActionCommand<GameObjectTransformParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectTransformParams>(
            CommandNames.GameObjectTransform,
            CommandHostScope.Editor,
            "Set position, rotation and/or scale, in local or world space, absolute or relative");

        protected override Dictionary<string, object> Execute(GameObjectTransformParams p) =>
            GameObjectService.SetTransform(p);
    }

    public sealed class GameObjectActivateCommand : EditorActionCommand<GameObjectActivateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectActivateParams>(
            CommandNames.GameObjectActivate,
            CommandHostScope.Editor,
            "Enable or disable a GameObject (omit 'active' to toggle)");

        protected override Dictionary<string, object> Execute(GameObjectActivateParams p) =>
            GameObjectService.Activate(p);
    }

    public sealed class GameObjectTagCommand : EditorActionCommand<GameObjectTagParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectTagParams>(
            CommandNames.GameObjectTag,
            CommandHostScope.Editor,
            "Assign a tag to a GameObject, optionally to its whole subtree");

        protected override Dictionary<string, object> Execute(GameObjectTagParams p) =>
            GameObjectService.SetTag(p);
    }

    public sealed class GameObjectLayerCommand : EditorActionCommand<GameObjectLayerParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectLayerParams>(
            CommandNames.GameObjectLayer,
            CommandHostScope.Editor,
            "Assign a layer to a GameObject, optionally to its whole subtree");

        protected override Dictionary<string, object> Execute(GameObjectLayerParams p) =>
            GameObjectService.SetLayer(p);
    }

    public sealed class GameObjectInfoCommand : EditorActionCommand<NodeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<NodeParams>(
            CommandNames.GameObjectInfo,
            CommandHostScope.Editor,
            "Everything about one GameObject: transform, tag, layer, components, children, prefab linkage");

        protected override Dictionary<string, object> Execute(NodeParams p) => GameObjectService.Info(p);
    }

    public sealed class GameObjectTreeCommand : EditorActionCommand<GameObjectTreeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectTreeParams>(
            CommandNames.GameObjectTree,
            CommandHostScope.Editor,
            "Hierarchy dump from a node or from the scene roots");

        protected override Dictionary<string, object> Execute(GameObjectTreeParams p) =>
            GameObjectService.Tree(p);
    }

    public sealed class GameObjectFindCommand : EditorActionCommand<GameObjectFindParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<GameObjectFindParams>(
            CommandNames.GameObjectFind,
            CommandHostScope.Editor,
            "Search GameObjects by name, tag, layer, component or path");

        protected override Dictionary<string, object> Execute(GameObjectFindParams p) =>
            GameObjectService.Find(p);
    }
}
