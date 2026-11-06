using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Prefab;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class PrefabOpenCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabStageOpenParams>(
            CommandNames.PrefabOpen,
            CommandHostScope.Editor,
            "Open a prefab in Prefab Mode (not required to edit it — pass '" + ParamKeys.Prefab + "' to the content commands instead)");

        public static object Execute(PrefabStageOpenParams p, CommandContext ctx) =>
            PrefabStageService.Open(p);
    }

    [UcpCommand]
    public static class PrefabSaveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.PrefabSave,
            CommandHostScope.Editor,
            "Save the prefab currently open in Prefab Mode");

        public static object Execute(CommandContext ctx) => PrefabStageService.Save();
    }

    [UcpCommand]
    public static class PrefabCloseCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PrefabCloseParams>(
            CommandNames.PrefabClose,
            CommandHostScope.Editor,
            "Leave Prefab Mode, saving first unless save=false");

        public static object Execute(PrefabCloseParams p, CommandContext ctx) =>
            PrefabStageService.Close(p);
    }

    [UcpCommand]
    public static class PrefabStageInfoCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.PrefabStageInfo,
            CommandHostScope.Editor,
            "Which prefab is open in Prefab Mode, and whether it has unsaved changes");

        public static object Execute(CommandContext ctx) => PrefabStageService.Info();
    }
}
