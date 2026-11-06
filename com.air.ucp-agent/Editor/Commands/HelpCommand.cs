using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    /// <summary>
    /// The Editor-scope command catalog. Keeping this a command rather than a checked-in markdown
    /// table is what stops the documented surface from drifting from the real one — the listing is
    /// generated from the same discovery pass that registers the commands.
    /// </summary>
    /// <remarks>
    /// Shares the <c>help</c> name with the Runtime-scope command; the host kind picks which one
    /// runs (<see cref="CommandRegistry.Find"/> is per host).
    /// </remarks>
    [UcpCommand]
    public static class HelpEditorCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<HelpParams>(
            CommandNames.Help,
            CommandHostScope.Editor,
            "List every editor command with its description and parameters",
            aliases: new[] { "commands", "?" })
        {
            RunsWhileBusy = true,
        };

        public static object Execute(HelpParams p, CommandContext ctx)
        {
            var host = CommandRegistry.EditorHost;
            if (string.Equals(p.Format, HelpParams.Markdown, StringComparison.OrdinalIgnoreCase))
                return InvokeCatalog.BuildMarkdown(host, p.Command);

            return string.IsNullOrWhiteSpace(p.Command)
                ? InvokeCatalog.BuildResponse(host)
                : InvokeCatalog.BuildCard(host, p.Command);
        }
    }
}
