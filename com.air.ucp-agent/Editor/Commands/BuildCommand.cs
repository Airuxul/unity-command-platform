using System;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class BuildCommand : CliCommand<BuildParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<BuildParams>(
            CommandNames.Build,
            CommandHostScope.Editor,
            "Build a player with BuildPipeline and return the report summary. The Editor answers nothing else until it is done. " +
            "Fails as build_failed with the build's errors.",
            defaultTimeoutMs: 30 * 60_000);

        public override void Run(BuildParams p)
        {
            try
            {
                var summary = BuildService.Build(p, out var succeeded);
                if (succeeded)
                {
                    CompleteSuccess(InvokeResult.Ok($"built {summary["output_path"]}", summary));
                    return;
                }

                CompleteFail(new CommandFailure(UcpErrorCodes.BuildFailed, $"Build {summary["result"]}.",
                    "data.messages has the build's errors; console has the full log.", summary));
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }
    }
}
