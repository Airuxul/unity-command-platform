using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabReadCommand : CliCommand<PrefabReadParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabReadParams>(
            CommandNames.PrefabRead,
            CommandHostScope.Editor,
            "List prefab nodes for an agent. Each node has path, name, active, and component full names. Pass node to return that node's rect and writable fields instead of the whole tree. path uses child names; a duplicated sibling name becomes #n (#1 is the first child). Pass path to prefab.edit. Does not save. prefab_stage_open means Prefab Mode may be ahead of the asset on disk.");

        public override void Run(PrefabReadParams p)
        {
            try
            {
                var data = PrefabEditService.Read(p.PrefabPath, p.Node);
                CompleteSuccess(InvokeResult.Ok("prefab.read completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }

    public sealed class PrefabReadParams
    {
        [CliParam("prefab_path", Description = "Prefab asset path, for example Assets/UI/Page.prefab", Required = true, AlternateKeys = "prefab")]
        public string PrefabPath { get; set; }

        [CliParam(Description = "When set, return rect and writable fields for this node only")]
        public string Node { get; set; }
    }
}
