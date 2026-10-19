using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class SceneEditCommand : CliCommand<SceneEditParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneEditParams>(
            CommandNames.SceneEdit,
            CommandHostScope.Editor,
            "Edit the active scene, or open scene_path when the active scene is clean, then save once. ops matches prefab.edit. Parent '.' creates a scene root. Play mode is rejected.");

        public override void Run(SceneEditParams p)
        {
            try
            {
                var data = SceneEditService.Edit(p.ScenePath, p.Ops);
                CompleteSuccess(InvokeResult.Ok("scene.edit completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }

    public sealed class SceneEditParams
    {
        [CliParam("scene_path", Description = "Scene asset to open when it is not already active", AlternateKeys = "scene")]
        public string ScenePath { get; set; }

        [CliParam(Description = "JSON array of hierarchy operations applied in order, then saved once", Required = true)]
        public object Ops { get; set; }
    }
}
