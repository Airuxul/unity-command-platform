using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Invoke;

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
    public sealed class HelpEditorCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.Help,
            CommandHostScope.Editor,
            "List every editor command with its description and parameters",
            aliases: new[] { "commands" });

        protected override Dictionary<string, object> Execute() => InvokeCatalog.BuildResponse(CommandRegistry.EditorHost);
    }
}
