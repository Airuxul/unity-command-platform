using Air.UcpAgent.Completion;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    /// <summary>Immediate refresh by default; compile=true upgrades job completion to compilation.</summary>
    [UcpCommand]
    public static class RefreshCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<RefreshParams>(
            CommandNames.Refresh,
            CommandHostScope.Editor,
            "Refresh AssetDatabase; use compile=true to request script compilation",
            defaultTimeoutMs: 180_000);

        public static object Execute(RefreshParams p, CommandContext ctx)
        {
            var data = AssetRefreshService.Refresh(p);
            if (!p.Compile)
            {
                ctx.Message = "refresh completed";
                return data;
            }

            // From here the job completes like `compile`, with the compile result.
            ctx.Defer(typeof(CompilationPolicy));
            ScriptCompilationService.RequestWithCompletion(ctx.CommandId);
            return null;
        }
    }
}
