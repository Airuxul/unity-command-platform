using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Runtime.Commands
{
    public class EchoRuntimeCommand : CliCommand<EchoParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<EchoParams>(
            CommandNames.Echo,
            CommandHostScope.Runtime,
            "Echo from Runtime host");

        public override void Run(EchoParams p)
        {
            var text = p?.Message ?? "ok";
            CompleteSuccess(InvokeResult.Ok("echo ok", new Dictionary<string, object>
            {
                ["channel"] = "runtime",
                ["message"] = text,
            }));
        }
    }
}
