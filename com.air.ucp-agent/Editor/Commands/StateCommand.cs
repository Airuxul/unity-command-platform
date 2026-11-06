using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class StateCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.State,
            CommandHostScope.Editor,
            "Editor and agent state snapshot")
        {
            RunsWhileBusy = true,
        };

        public static object Execute(CommandContext ctx)
        {
            ctx.Message = "state snapshot";
            return EditorStateProvider.ToManifestObject();
        }
    }
}
