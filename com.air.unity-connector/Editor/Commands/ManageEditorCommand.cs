using Air.UnityConnector.Invoke;
using Air.UnityConnector.Editor.Services;
using Air.UnityConnector.Params;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector.Commands
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
