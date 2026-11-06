using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Assets;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class AssetListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetListParams>(
            CommandNames.AssetList,
            CommandHostScope.Editor,
            "List assets under a folder, optionally filtered by type and name");

        public static object Execute(AssetListParams p, CommandContext ctx) => AssetService.List(p);
    }

    [UcpCommand]
    public static class AssetFindCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetFindParams>(
            CommandNames.AssetFind,
            CommandHostScope.Editor,
            "Search assets with a raw AssetDatabase filter string");

        public static object Execute(AssetFindParams p, CommandContext ctx) => AssetService.Find(p);
    }

    [UcpCommand]
    public static class AssetInfoCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetInfoParams>(
            CommandNames.AssetInfo,
            CommandHostScope.Editor,
            "Asset metadata: guid, type, importer, labels, sub-assets, dependencies");

        public static object Execute(AssetInfoParams p, CommandContext ctx) => AssetService.Info(p);
    }

    [UcpCommand]
    public static class AssetCreateFolderCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetCreateFolderParams>(
            CommandNames.AssetCreateFolder,
            CommandHostScope.Editor,
            "Create a folder, including any missing parents");

        public static object Execute(AssetCreateFolderParams p, AssetFileContext ctx) =>
            ctx.Changes.Creating(AssetPaths.RequireProjectPath(p.Path, ParamKeys.Path), () => AssetService.CreateFolder(p), isFolder: true);

        public static void Undo(AssetCreateFolderParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class AssetDeleteCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetDeleteParams>(
            CommandNames.AssetDelete,
            CommandHostScope.Editor,
            "Delete an asset or a whole folder");

        public static object Execute(AssetDeleteParams p, AssetFileContext ctx) =>
            ctx.Changes.Deleting(AssetPaths.RequireExisting(p.Path, ParamKeys.Path), ctx.Group, () => AssetService.Delete(p));

        public static void Undo(AssetDeleteParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class AssetMoveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetMoveParams>(
            CommandNames.AssetMove,
            CommandHostScope.Editor,
            "Move or rename an asset, keeping its GUID and references",
            aliases: new[] { "asset_rename" });

        public static object Execute(AssetMoveParams p, AssetFileContext ctx) =>
            ctx.Changes.Moving(AssetPaths.RequireExisting(p.Path, ParamKeys.Path), AssetPaths.RequireProjectPath(p.Target, ParamKeys.Target), () => AssetService.Move(p));

        public static void Undo(AssetMoveParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class AssetDuplicateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetMoveParams>(
            CommandNames.AssetDuplicate,
            CommandHostScope.Editor,
            "Copy an asset to a new path (new GUID)");

        public static object Execute(AssetMoveParams p, AssetFileContext ctx) =>
            ctx.Changes.Creating(AssetPaths.RequireProjectPath(p.Target, ParamKeys.Target), () => AssetService.Duplicate(p));

        public static void Undo(AssetMoveParams p, AssetFileContext ctx) => ctx.Changes.Undo();
    }

    [UcpCommand]
    public static class AssetDependenciesCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetDependenciesParams>(
            CommandNames.AssetDependencies,
            CommandHostScope.Editor,
            "List what an asset depends on, direct or transitive");

        public static object Execute(AssetDependenciesParams p, CommandContext ctx) =>
            AssetService.Dependencies(p);
    }

    [UcpCommand]
    public static class AssetGuidCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<AssetGuidParams>(
            CommandNames.AssetGuid,
            CommandHostScope.Editor,
            "Convert an asset path to its GUID, or a GUID back to its path");

        public static object Execute(AssetGuidParams p, CommandContext ctx) => AssetService.Guid(p);
    }
}
