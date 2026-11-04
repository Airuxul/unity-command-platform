using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabCreateCommand : EditorActionCommand<PrefabCreateParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabCreateParams>(
            CommandNames.PrefabCreate,
            CommandHostScope.Editor,
            "Save a node as a new prefab asset (inside another prefab this creates a nested prefab)");

        protected override Dictionary<string, object> Execute(PrefabCreateParams p) =>
            PrefabAssetService.Create(p);
    }

    public sealed class PrefabCreateEmptyCommand : EditorActionCommand<PrefabCreateEmptyParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabCreateEmptyParams>(
            CommandNames.PrefabCreateEmpty,
            CommandHostScope.Editor,
            "Create a prefab asset from scratch without touching any scene");

        protected override Dictionary<string, object> Execute(PrefabCreateEmptyParams p) =>
            PrefabAssetService.CreateEmpty(p);
    }

    public sealed class PrefabCreateVariantCommand : EditorActionCommand<PrefabVariantParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabVariantParams>(
            CommandNames.PrefabCreateVariant,
            CommandHostScope.Editor,
            "Create a prefab variant of a base prefab");

        protected override Dictionary<string, object> Execute(PrefabVariantParams p) =>
            PrefabAssetService.CreateVariant(p);
    }

    public sealed class PrefabDeleteCommand : EditorActionCommand<PrefabPathParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabPathParams>(
            CommandNames.PrefabDelete,
            CommandHostScope.Editor,
            "Delete a prefab asset");

        protected override Dictionary<string, object> Execute(PrefabPathParams p) =>
            PrefabAssetService.Delete(p);
    }

    public sealed class PrefabDuplicateCommand : EditorActionCommand<PrefabCopyParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabCopyParams>(
            CommandNames.PrefabDuplicate,
            CommandHostScope.Editor,
            "Copy a prefab asset to a new path (new GUID)");

        protected override Dictionary<string, object> Execute(PrefabCopyParams p) =>
            PrefabAssetService.Duplicate(p);
    }

    public sealed class PrefabMoveCommand : EditorActionCommand<PrefabCopyParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabCopyParams>(
            CommandNames.PrefabMove,
            CommandHostScope.Editor,
            "Move or rename a prefab asset, keeping its GUID and references",
            aliases: new[] { "prefab_rename" });

        protected override Dictionary<string, object> Execute(PrefabCopyParams p) =>
            PrefabAssetService.Move(p);
    }

    public sealed class PrefabListCommand : EditorActionCommand<PrefabListParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabListParams>(
            CommandNames.PrefabList,
            CommandHostScope.Editor,
            "List prefab assets under a folder");

        protected override Dictionary<string, object> Execute(PrefabListParams p) =>
            PrefabAssetService.List(p);
    }

    public sealed class PrefabInfoCommand : EditorActionCommand<PrefabInfoParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabInfoParams>(
            CommandNames.PrefabInfo,
            CommandHostScope.Editor,
            "Prefab asset metadata: guid, type, base prefab, nested prefabs, root components");

        protected override Dictionary<string, object> Execute(PrefabInfoParams p) =>
            PrefabAssetService.Info(p);
    }

    public sealed class PrefabTreeCommand : EditorActionCommand<PrefabTreeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabTreeParams>(
            CommandNames.PrefabTree,
            CommandHostScope.Editor,
            "Full hierarchy inside a prefab asset, with components and nested-prefab markers");

        protected override Dictionary<string, object> Execute(PrefabTreeParams p) =>
            PrefabAssetService.Tree(p);
    }
}
