using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
{
    public class ReserializeCommand : CliCommand<ReserializeParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ReserializeParams>(
            CommandNames.Reserialize,
            CommandHostScope.Editor,
            "Force reserialize assets (whole project or paths)");

        public override void Run(ReserializeParams p)
        {
            try
            {
                var data = ReserializeService.Reserialize(p);
                CompleteSuccess(InvokeResult.Ok("reserialize completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
