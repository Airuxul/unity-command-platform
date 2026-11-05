using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class UiSnapshotCommand : EditorActionCommand<UiSnapshotParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<UiSnapshotParams>(
            CommandNames.UiSnapshot,
            CommandHostScope.Editor,
            "Play mode only. List canvases with active state, Text and TextMeshProUGUI strings, input fields, and whether each Button can be clicked.");

        protected override Dictionary<string, object> Execute(UiSnapshotParams p) => UiSnapshotService.Capture(p);
    }
}
