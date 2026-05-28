using UnityCliConnector.Commands;
using UnityEditor.Compilation;

namespace UnityCliConnector.Builtin
{
    public class CompileCommand : CommandBase, ICommand<CompileParams>, ICommandDescriptorProvider
    {
        public CommandDescriptor Descriptor { get; } = new DeferredCommandDescriptor<CompileParams>(
            CommandNames.Compile,
            CommandScope.Editor,
            "Request script compilation (deferred)",
            CommandCompletionCatalog.CompletionCompilation,
            aliases: new[] { "recompile", "reload" },
            defaultTimeoutMs: 30000);

        public void Run(CompileParams p)
        {
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}
