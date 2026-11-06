using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class ScreenshotCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ScreenshotParams>(
            CommandNames.Screenshot,
            CommandHostScope.Editor,
            "Capture the Scene view, the Game view, or a UI canvas to PNG (view=ui for overlay UI)");

        public static object Execute(ScreenshotParams p, CommandContext ctx) =>
            ScreenshotService.Capture(p);
    }
}
