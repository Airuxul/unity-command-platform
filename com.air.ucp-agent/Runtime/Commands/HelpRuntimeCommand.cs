using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Runtime.Commands
{
    [UcpCommand]
    public static class HelpRuntimeCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<HelpParams>(
            CommandNames.Help,
            CommandHostScope.Runtime,
            "List every runtime command with its description and parameters",
            aliases: new[] { "?" })
        {
            RunsWhileBusy = true,
        };

        public static object Execute(HelpParams p, CommandContext ctx)
        {
            var host = CommandRegistry.RuntimeHost;
            if (string.Equals(p.Format, HelpParams.Markdown, System.StringComparison.OrdinalIgnoreCase))
                return InvokeCatalog.BuildMarkdown(host, p.Command);

            return string.IsNullOrWhiteSpace(p.Command)
                ? InvokeCatalog.BuildResponse(host)
                : InvokeCatalog.BuildCard(host, p.Command);
        }
    }
}
