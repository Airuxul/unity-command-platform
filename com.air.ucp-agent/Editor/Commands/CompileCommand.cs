using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using UnityEditor;

namespace Air.UcpAgent.Editor.Commands
{
    public class CompileCommand : CliCommand<CompileParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new DeferredInvokeDescriptor<CompileParams>(
            CommandNames.Compile,
            CommandHostScope.Editor,
            "Import changed files, then compile scripts (deferred)",
            InvokeCompletionCatalog.CompletionCompilation,
            aliases: new[] { "recompile", "reload" },
            defaultTimeoutMs: 20000);

        public override void Run(CompileParams p)
        {
            // Unity only imports changed files on focus (or with Auto Refresh on). Without this, a
            // compile requested while the editor is in the background rebuilds the old scripts and
            // still reports success.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ScriptCompilationService.RequestWithCompletion(CommandId);
        }
    }
}
