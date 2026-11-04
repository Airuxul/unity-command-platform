using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabOpenCommand : EditorActionCommand<PrefabStageOpenParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabStageOpenParams>(
            CommandNames.PrefabOpen,
            CommandHostScope.Editor,
            "Open a prefab in Prefab Mode (not required to edit it — pass 'prefab' to the content commands instead)");

        protected override Dictionary<string, object> Execute(PrefabStageOpenParams p) =>
            PrefabStageService.Open(p);
    }

    public sealed class PrefabSaveCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.PrefabSave,
            CommandHostScope.Editor,
            "Save the prefab currently open in Prefab Mode");

        protected override Dictionary<string, object> Execute() => PrefabStageService.Save();
    }

    public sealed class PrefabCloseCommand : EditorActionCommand<PrefabCloseParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabCloseParams>(
            CommandNames.PrefabClose,
            CommandHostScope.Editor,
            "Leave Prefab Mode, saving first unless save=false");

        protected override Dictionary<string, object> Execute(PrefabCloseParams p) =>
            PrefabStageService.Close(p);
    }

    public sealed class PrefabStageInfoCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.PrefabStageInfo,
            CommandHostScope.Editor,
            "Which prefab is open in Prefab Mode, and whether it has unsaved changes");

        protected override Dictionary<string, object> Execute() => PrefabStageService.Info();
    }
}
