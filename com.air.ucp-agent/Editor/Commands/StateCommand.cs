using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class StateCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.State,
            CommandHostScope.Editor,
            "Editor and agent state snapshot");

        public override void Run()
        {
            var data = EditorStateProvider.ToManifestObject();
            CompleteSuccess(InvokeResult.Ok("state snapshot", data));
        }
    }
}
