using System.Collections.Generic;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
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
