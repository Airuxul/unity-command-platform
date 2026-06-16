#if !UNITY_EDITOR
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Runtime.Commands
{
    public class PingRuntimeCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.Ping,
            CommandHostScope.Runtime,
            "Health check");

        public override void Run()
        {
            CompleteSuccess(InvokeResult.Ok("pong"));
        }
    }
}
#endif
