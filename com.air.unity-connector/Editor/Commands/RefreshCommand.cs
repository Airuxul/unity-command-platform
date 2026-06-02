using System.Collections.Generic;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    /// <summary>Immediate refresh by default; compile=true upgrades job completion to compilation.</summary>
    public class RefreshCommand : CliCommand<RefreshParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<RefreshParams>(
            CommandNames.Refresh,
            CommandHostScope.Editor,
            "Refresh AssetDatabase; use compile=true to request script compilation",
            defaultTimeoutMs: 20000);

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

                EditorJobStateManager.SetCompletionKind(
                    CommandId,
                    InvokeCompletionCatalog.CompletionCompilation);
                ScriptCompilationService.RequestWithCompletion(
                    CommandId,
                    result => CompleteSuccess(MergeRefreshResult(result, data)),
                    CompleteFail);
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }

        private static object MergeRefreshResult(object compileResult, Dictionary<string, object> refreshData)
        {
            if (compileResult is not InvokeResult compile || refreshData == null)
                return compileResult;

            var payload = compile.Payload as Dictionary<string, object> ?? new Dictionary<string, object>();
            foreach (var pair in refreshData)
                payload[pair.Key] = pair.Value;
            payload["compile_requested"] = true;

            return InvokeResult.Ok(compile.Message, payload);
        }
    }
}
