using UnityCliConnector.Commands;

namespace UnityCliConnector.Builtin
{
    public class ConnectorStateCommand : CommandBase, ICommand, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new CommandDescriptor(
            CommandNames.State,
            CommandScope.Editor,
            "Editor state snapshot");

        public void Run()
        {
            var data = EditorStateProvider.ToManifestObject();
            CompleteSuccess(data);
        }
    }
}
