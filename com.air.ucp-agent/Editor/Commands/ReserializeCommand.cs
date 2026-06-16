using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
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
