using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Assets;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class AssetListCommand : EditorActionCommand<AssetListParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetListParams>(
            CommandNames.AssetList,
            CommandHostScope.Editor,
            "List assets under a folder, optionally filtered by type and name");

        protected override Dictionary<string, object> Execute(AssetListParams p) => AssetService.List(p);
    }

    public sealed class AssetFindCommand : EditorActionCommand<AssetFindParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetFindParams>(
            CommandNames.AssetFind,
            CommandHostScope.Editor,
            "Search assets with a raw AssetDatabase filter string");

        protected override Dictionary<string, object> Execute(AssetFindParams p) => AssetService.Find(p);
    }

    public sealed class AssetInfoCommand : EditorActionCommand<AssetInfoParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetInfoParams>(
            CommandNames.AssetInfo,
            CommandHostScope.Editor,
            "Asset metadata: guid, type, importer, labels, sub-assets, dependencies");

        protected override Dictionary<string, object> Execute(AssetInfoParams p) => AssetService.Info(p);
    }

    public sealed class AssetCreateFolderCommand : EditorActionCommand<AssetCreateFolderParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetCreateFolderParams>(
            CommandNames.AssetCreateFolder,
            CommandHostScope.Editor,
            "Create a folder, including any missing parents");

        protected override Dictionary<string, object> Execute(AssetCreateFolderParams p) =>
            AssetService.CreateFolder(p);
    }

    public sealed class AssetDeleteCommand : EditorActionCommand<AssetDeleteParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetDeleteParams>(
            CommandNames.AssetDelete,
            CommandHostScope.Editor,
            "Delete an asset or a whole folder");

        protected override Dictionary<string, object> Execute(AssetDeleteParams p) => AssetService.Delete(p);
    }

    public sealed class AssetMoveCommand : EditorActionCommand<AssetMoveParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetMoveParams>(
            CommandNames.AssetMove,
            CommandHostScope.Editor,
            "Move or rename an asset, keeping its GUID and references",
            aliases: new[] { "asset_rename" });

        protected override Dictionary<string, object> Execute(AssetMoveParams p) => AssetService.Move(p);
    }

    public sealed class AssetDuplicateCommand : EditorActionCommand<AssetMoveParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetMoveParams>(
            CommandNames.AssetDuplicate,
            CommandHostScope.Editor,
            "Copy an asset to a new path (new GUID)");

        protected override Dictionary<string, object> Execute(AssetMoveParams p) => AssetService.Duplicate(p);
    }

    public sealed class AssetDependenciesCommand : EditorActionCommand<AssetDependenciesParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetDependenciesParams>(
            CommandNames.AssetDependencies,
            CommandHostScope.Editor,
            "List what an asset depends on, direct or transitive");

        protected override Dictionary<string, object> Execute(AssetDependenciesParams p) =>
            AssetService.Dependencies(p);
    }

    public sealed class AssetGuidCommand : EditorActionCommand<AssetGuidParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<AssetGuidParams>(
            CommandNames.AssetGuid,
            CommandHostScope.Editor,
            "Convert an asset path to its GUID, or a GUID back to its path");

        protected override Dictionary<string, object> Execute(AssetGuidParams p) => AssetService.Guid(p);
    }
}
