using Air.UcpAgent.Invoke;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class PingCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.Ping,
            CommandHostScope.Any,
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
