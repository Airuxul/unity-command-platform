using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
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
