using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ProfilerCommand : EditorActionCommand<ProfilerParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ProfilerParams>(
            CommandNames.Profiler,
            CommandHostScope.Editor,
            "Unity Profiler: hierarchy, enable, disable, status, clear");

        protected override Dictionary<string, object> Execute(ProfilerParams p) => ProfilerHierarchyService.Execute(p);

        protected override string Message(ProfilerParams p) => "profiler completed";
    }
}
