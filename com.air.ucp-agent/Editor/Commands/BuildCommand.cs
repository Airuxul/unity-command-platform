using System;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class BuildCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<BuildParams>(
            CommandNames.Build,
            CommandHostScope.Editor,
            "Build a player with BuildPipeline and return the report summary. The Editor answers nothing else until it is done. " +
            "Fails as build_failed with the build's errors.",
            defaultTimeoutMs: 30 * 60_000);

        public static object Execute(BuildParams p, CommandContext ctx)
        {
            var summary = BuildService.Build(p, out var succeeded);
            if (succeeded)
            {
                ctx.Message = $"built {summary["output_path"]}";
                return summary;
            }

            ctx.Fail(new CommandFailure(UcpErrorCodes.BuildFailed, $"Build {summary["result"]}.",
                "data.messages has the build's errors; console has the full log.", summary));
            return null;
        }
    }
}
