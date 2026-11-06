#if !UNITY_EDITOR
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Runtime.Commands
{
    [UcpCommand]
    public static class PingRuntimeCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.Ping,
            CommandHostScope.Runtime,
            "Health check")
        {
            RunsWhileBusy = true,
        };

        public static object Execute(CommandContext ctx)
        {
            ctx.Message = "pong";
            return null;
        }
    }
}
#endif
