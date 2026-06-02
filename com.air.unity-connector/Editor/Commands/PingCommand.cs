using Air.UnityConnector.Invoke;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class PingCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.Ping,
            CommandHostScope.Any,
            "Health check");

        public override void Run()
        {
            CompleteSuccess(InvokeResult.Ok("pong"));
        }
    }
}
