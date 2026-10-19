using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class SceneReadCommand : CliCommand<SceneReadParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneReadParams>(
            CommandNames.SceneRead,
            CommandHostScope.Editor,
            "List the active scene, or open scene_path when the active scene is clean. Each node has path, name, active, and component full names. Pass node for that node's rect and writable fields. '.' is not a scene object; root names are the first path segment. #n is the nth sibling. Does not save.");

        public override void Run(SceneReadParams p)
        {
            try
            {
                var data = SceneEditService.Read(p.ScenePath, p.Node);
                CompleteSuccess(InvokeResult.Ok("scene.read completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }

    public sealed class SceneReadParams
    {
        [CliParam("scene_path", Description = "Scene asset to open when it is not already active", AlternateKeys = "scene")]
        public string ScenePath { get; set; }

        [CliParam(Description = "When set, return rect and writable fields for this node only")]
        public string Node { get; set; }
    }
}
