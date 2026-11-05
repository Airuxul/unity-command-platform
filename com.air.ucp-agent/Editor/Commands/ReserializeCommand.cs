using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ReserializeCommand : EditorActionCommand<ReserializeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ReserializeParams>(
            CommandNames.Reserialize,
            CommandHostScope.Editor,
            "Force reserialize assets (whole project or paths)");

        protected override Dictionary<string, object> Execute(ReserializeParams p) => ReserializeService.Reserialize(p);

        protected override string Message(ReserializeParams p) => "reserialize completed";
    }
}
