using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class EchoEditorCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<EchoParams>(
            CommandNames.Echo,
            CommandHostScope.Editor,
            "Echo from Editor host")
        {
            RunsWhileBusy = true,
        };

        public static object Execute(EchoParams p, CommandContext ctx)
        {
            ctx.Message = "echo ok";
            return new Dictionary<string, object>
            {
                ["channel"] = "editor",
                ["message"] = p?.Message ?? "ok",
            };
        }
    }
}
