using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class UiSnapshotCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.UiSnapshot,
            CommandHostScope.Editor,
            "Play mode only. List canvases with active state, Text and TextMeshProUGUI strings, and whether each Button can be clicked.");

        public override void Run()
        {
            try
            {
                var data = UiSnapshotService.Capture();
                CompleteSuccess(InvokeResult.Ok("ui.snapshot completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
