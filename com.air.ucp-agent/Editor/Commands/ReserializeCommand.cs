using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class ReserializeCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ReserializeParams>(
            CommandNames.Reserialize,
            CommandHostScope.Editor,
            "Force reserialize assets (whole project or paths)");

        public static object Execute(ReserializeParams p, CommandContext ctx)
        {
            ctx.Message = "reserialize completed";
            return ReserializeService.Reserialize(p);
        }
    }
}
