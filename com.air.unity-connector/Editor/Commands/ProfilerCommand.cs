using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
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
