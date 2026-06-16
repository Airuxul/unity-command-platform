using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Runtime.Shell;

namespace Air.UcpAgent.Runtime.Commands
{
    public class HelpRuntimeCommand : CliCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.Help,
            CommandHostScope.Runtime,
            "List available runtime commands",
            aliases: new[] { "?" });

        public override void Run()
        {
            CompleteSuccess(InvokeResult.Ok(string.Join("\n", ShellHintService.BuildHelpLines())));
        }
    }
}
