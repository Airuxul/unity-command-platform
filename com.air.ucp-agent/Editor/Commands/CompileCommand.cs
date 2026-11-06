using System;
using Air.UcpAgent.Completion;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using UnityEditor;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class CompileCommand
    {
        public static readonly Type CompletionPolicy = typeof(CompilationPolicy);

        public static readonly InvokeDescriptor Descriptor = new DeferredInvokeDescriptor<CompileParams>(
            CommandNames.Compile,
            CommandHostScope.Editor,
            "Import changed files, then compile scripts (deferred)",
            CompletionPolicy,
            aliases: new[] { "recompile", "reload" },
            defaultTimeoutMs: 180_000);

        public static object Execute(CompileParams p, CommandContext ctx)
        {
            // Unity only imports changed files on focus (or with Auto Refresh on). Without this, a
            // compile requested while the editor is in the background rebuilds the old scripts and
            // still reports success.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            ScriptCompilationService.RequestWithCompletion(ctx.CommandId);
            return null;
        }
    }
}
