using Air.UnityConnector.Invoke;
using Air.UnityConnector;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class ConnectorStateCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.State,
            CommandHostScope.Editor,
            "Editor state snapshot");

        public override void Run()
        {
            var data = EditorStateProvider.ToManifestObject();
            CompleteSuccess(InvokeResult.Ok("state snapshot", data));
        }
    }
}
