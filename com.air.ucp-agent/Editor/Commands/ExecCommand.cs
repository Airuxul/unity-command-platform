using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ExecCommand : CliCommand<ExecParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ExecParams>(
            CommandNames.Exec,
            CommandHostScope.Editor,
            "Compile and execute arbitrary C# in Editor context");

        public override void Run(ExecParams p)
        {
            try
            {
                var data = CsharpExecutor.Execute(p);
                CompleteSuccess(InvokeResult.Ok("exec completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
