using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class ScreenshotCommand : EditorActionCommand<ScreenshotParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ScreenshotParams>(
            CommandNames.Screenshot,
            CommandHostScope.Editor,
            "Capture the Scene view, the Game view, or a UI canvas to PNG (view=ui for overlay UI)");

        protected override Dictionary<string, object> Execute(ScreenshotParams p) =>
            ScreenshotService.Capture(p);
    }
}
