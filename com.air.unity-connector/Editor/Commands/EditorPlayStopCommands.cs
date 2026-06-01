using UnityCliConnector.Commands;
using UnityEditor;
using UnityCliConnector.Params;

namespace UnityCliConnector.Commands
{
    public class EditorPlayCommand : CommandBase, ICommand<PlayParams>, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new DeferredCommandDescriptor<PlayParams>(
            CommandNames.Play,
            CommandScope.Editor,
            "Enter Play Mode (deferred)",
            CommandCompletionCatalog.CompletionEnterPlay,
            defaultTimeoutMs: 60000);

        public void Run(PlayParams p)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    public class EditorStopCommand : CommandBase, ICommand<StopParams>, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new DeferredCommandDescriptor<StopParams>(
            CommandNames.Stop,
            CommandScope.Editor,
            "Exit Play Mode (deferred)",
            CommandCompletionCatalog.CompletionExitPlay,
            defaultTimeoutMs: 60000);

        public void Run(StopParams p)
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
