using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class UiSnapshotCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.UiSnapshot,
            CommandHostScope.Editor,
            "Play mode only. List canvases with active state, Text and TextMeshProUGUI strings, and whether each Button can be clicked.");

        protected override Dictionary<string, object> Execute() => UiSnapshotService.Capture();
    }
}
