using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class SelectionGetCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.SelectionGet,
            CommandHostScope.Editor,
            "What is selected in the Editor right now, split into assets and scene nodes");

        protected override Dictionary<string, object> Execute() => SelectionService.Get();
    }

    public sealed class SelectionSetCommand : EditorActionCommand<SelectionSetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SelectionSetParams>(
            CommandNames.SelectionSet,
            CommandHostScope.Editor,
            "Select assets and/or scene nodes so the user can see them");

        protected override Dictionary<string, object> Execute(SelectionSetParams p) => SelectionService.Set(p);
    }

    public sealed class SelectionClearCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.SelectionClear,
            CommandHostScope.Editor,
            "Clear the Editor selection");

        protected override Dictionary<string, object> Execute() => SelectionService.Clear();
    }
}
