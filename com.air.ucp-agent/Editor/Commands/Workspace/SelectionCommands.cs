using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class SelectionGetCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.SelectionGet,
            CommandHostScope.Editor,
            "What is selected in the Editor right now, split into assets and scene nodes");

        public static object Execute(CommandContext ctx) => SelectionService.Get();
    }

    [UcpCommand]
    public static class SelectionSetCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SelectionSetParams>(
            CommandNames.SelectionSet,
            CommandHostScope.Editor,
            "Select assets and/or scene nodes so the user can see them");

        public static object Execute(SelectionSetParams p, CommandContext ctx) => SelectionService.Set(p);
    }

    [UcpCommand]
    public static class SelectionClearCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.SelectionClear,
            CommandHostScope.Editor,
            "Clear the Editor selection");

        public static object Execute(CommandContext ctx) => SelectionService.Clear();
    }
}
