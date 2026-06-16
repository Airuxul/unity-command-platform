using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ProfilerCommand : CliCommand<ProfilerParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ProfilerParams>(
            CommandNames.Profiler,
            CommandHostScope.Editor,
            "Unity Profiler: hierarchy, enable, disable, status, clear");

        public override void Run(ProfilerParams p)
        {
            try
            {
                var data = ProfilerHierarchyService.Execute(p);
                CompleteSuccess(InvokeResult.Ok("profiler completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
