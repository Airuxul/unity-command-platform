using UnityCliConnector.Commands;
using UnityCliConnector.Params;

namespace UnityCliConnector.Commands
{
    public class EchoRuntimeCommand : CommandBase, ICommand<EchoParams>, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new CommandDescriptor<EchoParams>(
            CommandNames.Echo,
            CommandScope.Runtime,
            "Echo from Runtime host");

        public void Run(EchoParams p)
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["channel"] = "runtime",
                ["message"] = p.Message ?? "ok",
            };
            CompleteSuccess(data);
        }
    }
}
