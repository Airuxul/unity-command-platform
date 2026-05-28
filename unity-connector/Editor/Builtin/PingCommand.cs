using UnityCliConnector.Commands;

namespace UnityCliConnector.Builtin
{
    public class PingCommand : CommandBase, ICommand, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new CommandDescriptor(
            CommandNames.Ping,
            CommandScope.Any,
            "Health check");

        public void Run()
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["pong"] = true,
                ["host"] = "editor",
            };
            CompleteSuccess(data);
        }
    }
}
