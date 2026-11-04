namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Picks what a content command edits. Supply <c>prefab</c> to edit a prefab asset in place
    /// (no scene and no Prefab Mode needed), <c>scene</c> to pick among loaded scenes, or neither
    /// to use the open Prefab Mode stage if there is one and the active scene otherwise.
    /// </summary>
    public abstract class EditTargetParams
    {
        [CliParam(Description = "prefab asset to edit instead of a scene, e.g. Assets/P/Player.prefab",
            AlternateKeys = "prefab_path")]
        public string Prefab { get; set; }

        [CliParam(Description = "loaded scene name or path (default: active scene)",
            AlternateKeys = "scene_name")]
        public string Scene { get; set; }
    }

    /// <summary>An <see cref="EditTargetParams"/> that also addresses one node inside the target.</summary>
    public abstract class NodeTargetParams : EditTargetParams
    {
        [CliParam(Description = "hierarchy path, e.g. Root/Body/Arm; use Name[1] to pick among same-named siblings",
            AlternateKeys = "gameobject,go,node_path")]
        public string Node { get; set; }

        [CliParam(Description = "Unity instance id, as an alternative to 'node'",
            AlternateKeys = "instance_id")]
        public int? Id { get; set; }
    }
}
