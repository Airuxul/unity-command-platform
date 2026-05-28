using UnityCliConnector.Commands;

namespace UnityCliConnector.Builtin
{
    public class EchoEditorCommand : CommandBase, ICommand<EchoParams>, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new CommandDescriptor<EchoParams>(
            CommandNames.Echo,
            CommandScope.Editor,
            "Echo from Editor host");

        public void Run(EchoParams p)
        {
            var data = new System.Collections.Generic.Dictionary<string, object>
            {
                ["channel"] = "editor",
                ["message"] = p.Message ?? "ok",
            };
            CompleteSuccess(data);
        }
    }
}
