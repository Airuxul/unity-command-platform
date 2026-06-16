using Air.UcpAgent.Invoke;
using UnityEditor;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class EditorPlayCommand : CliCommand<PlayParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<PlayParams>(
            CommandNames.Play,
            CommandHostScope.Editor,
            "Enter Play Mode (deferred)",
            InvokeCompletionCatalog.CompletionEnterPlay,
            defaultTimeoutMs: 20000);

        public override void Run(PlayParams p)
        {
            EditorApplication.EnterPlaymode();
        }
    }

    public class EditorStopCommand : CliCommand<StopParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<StopParams>(
            CommandNames.Stop,
            CommandHostScope.Editor,
            "Exit Play Mode (deferred)",
            InvokeCompletionCatalog.CompletionExitPlay,
            defaultTimeoutMs: 20000);

        public override void Run(StopParams p)
        {
            EditorApplication.ExitPlaymode();
        }
    }
}
