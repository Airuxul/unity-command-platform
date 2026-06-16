using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class BuildCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.Build,
            CommandHostScope.Editor,
            "Build project (skeleton)");

        public override void Run()
        {
            CompleteSuccess(
                InvokeResult.Ok(
                    "build_skeleton_ok",
                    new Dictionary<string, object>
                    {
                        { "implemented", false },
                        { "hint", "Wire BuildPipeline in a future phase" },
                    }));
        }
    }
}
