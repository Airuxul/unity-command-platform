using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ManageEditorCommand : CliCommand<ManageEditorParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ManageEditorParams>(
            CommandNames.Manage,
            CommandHostScope.Editor,
            "Editor control: play, stop, pause, refresh, tags, layers, tools");

        public override void Run(ManageEditorParams p)
        {
            try
            {
                var data = EditorManageService.Execute(p);
                CompleteSuccess(InvokeResult.Ok("manage completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }
}
