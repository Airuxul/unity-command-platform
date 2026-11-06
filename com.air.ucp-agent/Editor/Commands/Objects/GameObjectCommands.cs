using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    // Every command here takes an optional `prefab` argument. With it they edit that prefab asset
    // in place; without it they edit the open scene. One implementation, both scopes.

    [UcpCommand]
    public static class GameObjectCreateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectCreateParams>(
            CommandNames.GameObjectCreate,
            CommandHostScope.Editor,
            "Create a GameObject in a scene or inside a prefab asset");

        public static object Execute(GameObjectCreateParams p, EditTargetContext ctx) =>
            GameObjectService.Create(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectDeleteCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<NodeParams>(
            CommandNames.GameObjectDelete,
            CommandHostScope.Editor,
            "Delete a GameObject and its children");

        public static object Execute(NodeParams p, EditTargetContext ctx) => GameObjectService.Delete(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectDuplicateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectDuplicateParams>(
            CommandNames.GameObjectDuplicate,
            CommandHostScope.Editor,
            "Duplicate a GameObject, optionally under a different parent");

        public static object Execute(GameObjectDuplicateParams p, EditTargetContext ctx) =>
            GameObjectService.Duplicate(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectRenameCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectRenameParams>(
            CommandNames.GameObjectRename,
            CommandHostScope.Editor,
            "Rename a GameObject");

        public static object Execute(GameObjectRenameParams p, EditTargetContext ctx) =>
            GameObjectService.Rename(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectReparentCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectReparentParams>(
            CommandNames.GameObjectReparent,
            CommandHostScope.Editor,
            "Move a GameObject under a new parent and/or sibling index");

        public static object Execute(GameObjectReparentParams p, EditTargetContext ctx) =>
            GameObjectService.Reparent(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectTransformCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectTransformParams>(
            CommandNames.GameObjectTransform,
            CommandHostScope.Editor,
            "Set position, rotation and/or scale, in local or world space, absolute or relative");

        public static object Execute(GameObjectTransformParams p, EditTargetContext ctx) =>
            GameObjectService.SetTransform(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectActivateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectActivateParams>(
            CommandNames.GameObjectActivate,
            CommandHostScope.Editor,
            "Enable or disable a GameObject (omit 'active' to toggle)");

        public static object Execute(GameObjectActivateParams p, EditTargetContext ctx) =>
            GameObjectService.Activate(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectTagCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectTagParams>(
            CommandNames.GameObjectTag,
            CommandHostScope.Editor,
            "Assign a tag to a GameObject, optionally to its whole subtree");

        public static object Execute(GameObjectTagParams p, EditTargetContext ctx) =>
            GameObjectService.SetTag(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectLayerCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectLayerParams>(
            CommandNames.GameObjectLayer,
            CommandHostScope.Editor,
            "Assign a layer to a GameObject, optionally to its whole subtree");

        public static object Execute(GameObjectLayerParams p, EditTargetContext ctx) =>
            GameObjectService.SetLayer(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectInfoCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<NodeParams>(
            CommandNames.GameObjectInfo,
            CommandHostScope.Editor,
            "Everything about one GameObject: transform, tag, layer, components, children, prefab linkage")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(NodeParams p, EditTargetContext ctx) => GameObjectService.Info(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectTreeCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectTreeParams>(
            CommandNames.GameObjectTree,
            CommandHostScope.Editor,
            "Hierarchy dump from a node or from the scene roots")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(GameObjectTreeParams p, EditTargetContext ctx) =>
            GameObjectService.Tree(ctx.Scope, p);
    }

    [UcpCommand]
    public static class GameObjectFindCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<GameObjectFindParams>(
            CommandNames.GameObjectFind,
            CommandHostScope.Editor,
            "Search GameObjects by name, tag, layer, component or path")
        {
            ReadsTargetOnly = true,
        };

        public static object Execute(GameObjectFindParams p, EditTargetContext ctx) =>
            GameObjectService.Find(ctx.Scope, p);
    }
}
