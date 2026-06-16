using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
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
