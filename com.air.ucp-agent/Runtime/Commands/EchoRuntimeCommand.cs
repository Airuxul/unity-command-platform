using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Runtime.Commands
{
    [UcpCommand]
    public static class EchoRuntimeCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<EchoParams>(
            CommandNames.Echo,
            CommandHostScope.Runtime,
            "Echo from Runtime host")
        {
            RunsWhileBusy = true,
        };

        public static object Execute(EchoParams p, CommandContext ctx)
        {
            ctx.Message = "echo ok";
            return new Dictionary<string, object>
            {
                ["channel"] = "runtime",
                ["message"] = p?.Message ?? "ok",
            };
        }
    }
}
