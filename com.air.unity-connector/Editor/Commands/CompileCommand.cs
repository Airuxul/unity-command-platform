using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class CompileCommand : CliCommand<CompileParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<CompileParams>(
            CommandNames.Compile,
            CommandHostScope.Editor,
            "Request script compilation (deferred)",
            InvokeCompletionCatalog.CompletionCompilation,
            aliases: new[] { "recompile", "reload" },
            defaultTimeoutMs: 20000);

        public override void Run(CompileParams p)
        {
            ScriptCompilationService.RequestWithCompletion(
                CommandId,
                CompleteSuccess,
                CompleteFail);
        }
    }
}
