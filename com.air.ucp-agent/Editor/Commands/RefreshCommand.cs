using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    /// <summary>Immediate refresh by default; compile=true upgrades job completion to compilation.</summary>
    public class RefreshCommand : CliCommand<RefreshParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<RefreshParams>(
            CommandNames.Refresh,
            CommandHostScope.Editor,
            "Refresh AssetDatabase; use compile=true to request script compilation",
            defaultTimeoutMs: 180_000);

        public override void Run(RefreshParams p)
        {
            try
            {
                var data = AssetRefreshService.Refresh(p);
                if (!p.Compile)
                {
                    CompleteSuccess(InvokeResult.Ok("refresh completed", data));
                    return;
                }

                // From here the job completes like `compile`, with the compile result.
                Defer(InvokeCompletionCatalog.CompletionCompilation);
                ScriptCompilationService.RequestWithCompletion(CommandId);
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
