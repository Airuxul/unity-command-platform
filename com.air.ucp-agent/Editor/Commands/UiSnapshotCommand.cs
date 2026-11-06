using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class UiSnapshotCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<UiSnapshotParams>(
            CommandNames.UiSnapshot,
            CommandHostScope.Editor,
            "Play mode only. List canvases with active state, Text and TextMeshProUGUI strings, input fields, and whether each Button can be clicked.");

        public static object Execute(UiSnapshotParams p, CommandContext ctx) => UiSnapshotService.Capture(p);
    }
}
