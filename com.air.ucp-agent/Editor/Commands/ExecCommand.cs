using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class ExecCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ExecParams>(
            CommandNames.Exec,
            CommandHostScope.Editor,
            "Execute arbitrary C# in the Editor. Compile changed scripts first; this command runs the scripts already loaded.");

        public static object Execute(ExecParams p, CommandContext ctx)
        {
            ctx.Message = "exec completed";
            return CsharpExecutor.Execute(p);
        }
    }
}
