using System.Collections.Generic;
using Air.UcpAgent.Editor.Services.Objects;

namespace Air.UcpAgent.Params
{
    public sealed class PrefabEditParams
    {
        [CliParam(ParamKeys.Prefab, Description = "prefab asset to edit, e.g. Assets/UI/Page.prefab", Required = true,
            AlternateKeys = "prefab_path")]
        public string Prefab { get; set; }

        [CliParam(Description = "ops run in order, each {\"command\":\"gameobject_create\",\"args\":{..}}; "
            + "saved once if all succeed, nothing kept if one fails", Required = true)]
        public List<EditBatchOp> Ops { get; set; }

        [CliParam("dry_run", Description = "run every op and report the results, then keep nothing",
            AlternateKeys = "dryrun")]
        public bool DryRun { get; set; }
    }

    public sealed class SceneEditParams
    {
        [CliParam(ParamKeys.Scene, Description = "loaded scene name or path (default: active scene)", AlternateKeys = "scene_name")]
        public string Scene { get; set; }

        [CliParam(Description = "ops run in order, each {\"command\":\"gameobject_create\",\"args\":{..}}; "
            + "undone together if one fails", Required = true)]
        public List<EditBatchOp> Ops { get; set; }

        [CliParam("dry_run", Description = "run every op and report the results, then keep nothing",
            AlternateKeys = "dryrun")]
        public bool DryRun { get; set; }
    }
}
