using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class PrefabEditCommand : CliCommand<PrefabEditParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PrefabEditParams>(
            CommandNames.PrefabEdit,
            CommandHostScope.Editor,
            "Edit a prefab in one save. Run prefab.read first. ops is a JSON array: add_component {node,type}, set_reference {node,type,field,target,target_type?} (target is a node path or an Assets/ path), rename {node,name}, create_node {parent,name}, delete_node {node}, delete_component {node,type}, set_property {node,type,field,value}, set_rect {node,anchor_min?,anchor_max?,pivot?,anchored_position?,size_delta?}, reparent {node,parent,sibling?}, instantiate {parent,prefab_path,name?}. Node path '.' is the root, '/' separates segments, #n is the nth child. A short component name that matches one UnityEngine.UI type uses that type; otherwise pass the full name.");

        public override void Run(PrefabEditParams p)
        {
            try
            {
                var data = PrefabEditService.Edit(p.PrefabPath, p.Ops);
                CompleteSuccess(InvokeResult.Ok("prefab.edit completed", data));
            }
            catch (System.Exception ex)
            {
                CompleteFail(ex.Message);
            }
        }
    }

    public sealed class PrefabEditParams
    {
        [CliParam("prefab_path", Description = "Prefab asset path, for example Assets/UI/Page.prefab", Required = true, AlternateKeys = "prefab")]
        public string PrefabPath { get; set; }

        [CliParam(Description = "JSON array of hierarchy operations applied in order, then saved once", Required = true)]
        public object Ops { get; set; }
    }
}
