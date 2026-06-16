using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
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
