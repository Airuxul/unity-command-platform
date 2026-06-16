using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class EchoEditorCommand : CliCommand<EchoParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<EchoParams>(
            CommandNames.Echo,
            CommandHostScope.Editor,
            "Echo from Editor host");

        public override void Run(EchoParams p)
        {
            var text = p?.Message ?? "ok";
            CompleteSuccess(InvokeResult.Ok("echo ok", new Dictionary<string, object>
            {
                ["channel"] = "editor",
                ["message"] = text,
            }));
        }
    }
}
