using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Editor.Services.Assets;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class PrefabCreateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabCreateParams>(
            CommandNames.PrefabCreate,
            CommandHostScope.Editor,
            "Save a node as a new prefab asset (inside another prefab this creates a nested prefab)");

        public static object Execute(PrefabCreateParams p, PrefabCreateContext ctx)
        {
            var path = AssetPaths.RequirePrefabPath(p.Path, ParamKeys.Path);
            return ctx.Changes.Creating(path, () => PrefabAssetService.Create(ctx.Scope, p));
        }

        /// <summary>
        /// The step's undo group takes back the connection in the scene. The new .prefab file and
        /// any folder created for it are not in it, so they are removed here.
        /// </summary>
        public static void Undo(PrefabCreateParams p, PrefabCreateContext ctx) => ctx.Changes.Undo();
    }

    public sealed class PrefabCreateContext : EditTargetContext
    {
        public AssetChanges Changes = new AssetChanges();
    }

    [UcpCommand]
    public static class PrefabCreateEmptyCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabCreateEmptyParams>(
            CommandNames.PrefabCreateEmpty,
            CommandHostScope.Editor,
            "Create a prefab asset from scratch without touching any scene");

        public static object Execute(PrefabCreateEmptyParams p, AssetFileContext ctx) =>
            ctx.Changes.Creating(AssetPaths.RequirePrefabPath(p.Path, ParamKeys.Path), () => PrefabAssetService.CreateEmpty(p));

        public static void Undo(PrefabCreateEmptyParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class PrefabCreateVariantCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabVariantParams>(
            CommandNames.PrefabCreateVariant,
            CommandHostScope.Editor,
            "Create a prefab variant of a base prefab");

        public static object Execute(PrefabVariantParams p, AssetFileContext ctx) =>
            ctx.Changes.Creating(AssetPaths.RequirePrefabPath(p.Target, ParamKeys.Target), () => PrefabAssetService.CreateVariant(p));

        public static void Undo(PrefabVariantParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class PrefabDeleteCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabPathParams>(
            CommandNames.PrefabDelete,
            CommandHostScope.Editor,
            "Delete a prefab asset");

        public static object Execute(PrefabPathParams p, AssetFileContext ctx) =>
            ctx.Changes.Deleting(AssetPaths.RequireExisting(p.Path, ParamKeys.Path), ctx.Group, () => PrefabAssetService.Delete(p));

        public static void Undo(PrefabPathParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class PrefabDuplicateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabCopyParams>(
            CommandNames.PrefabDuplicate,
            CommandHostScope.Editor,
            "Copy a prefab asset to a new path (new GUID)");

        public static object Execute(PrefabCopyParams p, AssetFileContext ctx) =>
            ctx.Changes.Creating(AssetPaths.RequirePrefabPath(p.Target, ParamKeys.Target), () => PrefabAssetService.Duplicate(p));

        public static void Undo(PrefabCopyParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class PrefabMoveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabCopyParams>(
            CommandNames.PrefabMove,
            CommandHostScope.Editor,
            "Move or rename a prefab asset, keeping its GUID and references",
            aliases: new[] { "prefab_rename" });

        public static object Execute(PrefabCopyParams p, AssetFileContext ctx) =>
            ctx.Changes.Moving(AssetPaths.RequireExisting(p.Path, ParamKeys.Path), AssetPaths.RequirePrefabPath(p.Target, ParamKeys.Target), () => PrefabAssetService.Move(p));

        public static void Undo(PrefabCopyParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class PrefabListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabListParams>(
            CommandNames.PrefabList,
            CommandHostScope.Editor,
            "List prefab assets under a folder");

        public static object Execute(PrefabListParams p, CommandContext ctx) =>
            PrefabAssetService.List(p);
    }

    [UcpCommand]
    public static class PrefabInfoCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabInfoParams>(
            CommandNames.PrefabInfo,
            CommandHostScope.Editor,
            "Prefab asset metadata: guid, type, base prefab, nested prefabs, root components");

        public static object Execute(PrefabInfoParams p, CommandContext ctx) =>
            PrefabAssetService.Info(p);
    }

    [UcpCommand]
    public static class PrefabTreeCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabTreeParams>(
            CommandNames.PrefabTree,
            CommandHostScope.Editor,
            "Full hierarchy inside a prefab asset, with components and nested-prefab markers");

        public static object Execute(PrefabTreeParams p, CommandContext ctx) =>
            PrefabAssetService.Tree(p);
    }
}
