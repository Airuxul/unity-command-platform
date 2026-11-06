using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class ProfilerCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ProfilerParams>(
            CommandNames.Profiler,
            CommandHostScope.Editor,
            "Unity Profiler: hierarchy, enable, disable, status, clear");

        public static object Execute(ProfilerParams p, CommandContext ctx)
        {
            ctx.Message = "profiler completed";
            return ProfilerHierarchyService.Execute(p);
        }
    }
}
