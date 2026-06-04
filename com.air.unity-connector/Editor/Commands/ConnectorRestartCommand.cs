using Air.UnityConnector.Cli;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Server;

namespace Air.UnityConnector.Commands
{
    public class ConnectorRestartCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.ConnectorRestart,
            CommandHostScope.Editor,
            "Drain and restart the Editor HTTP listener (supervisor controlled)");

        public override void Run()
        {
            EditorServerSupervisor.Instance.RequestControlledRestart();
            CompleteSuccess(InvokeResult.Ok("connector restart scheduled"));
        }
    }
}
