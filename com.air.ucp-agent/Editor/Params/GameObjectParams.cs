using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Params
{
    public sealed class GameObjectCreateParams : EditTargetParams
    {
        [CliParam(Description = "name of the new GameObject", Required = true)]
        public string Name { get; set; }

        [CliParam(Description = "parent node path (omit for top level / prefab root)")]
        public string Parent { get; set; }

        [CliParam(Description = "built-in shape to create",
            AllowedValues = "empty|cube|sphere|capsule|cylinder|plane|quad")]
        public string Primitive { get; set; } = "empty";

        [CliParam(Description = "local position as [x,y,z], {x,y,z}, or a single number")]
        public JToken Position { get; set; }

        [CliParam(Description = "local euler angles as [x,y,z] (or [x,y,z,w] for a raw quaternion)")]
        public JToken Rotation { get; set; }

        [CliParam(Description = "local scale as [x,y,z] or a single number")]
        public JToken Scale { get; set; }

        [CliParam(Description = "tag to assign")]
        public string Tag { get; set; }

        [CliParam(Description = "layer name or index")]
        public string Layer { get; set; }

        [CliParam(Description = "component type names to add, e.g. [\"Rigidbody\",\"BoxCollider\"]")]
        public string[] Components { get; set; }

        [CliParam(Description = "initial active state (default: true)")]
        public bool? Active { get; set; }
    }

    /// <summary>Commands that only need to identify a node: delete, info.</summary>
    public sealed class NodeParams : NodeTargetParams
    {
    }

    public sealed class GameObjectDuplicateParams : NodeTargetParams
    {
        [CliParam(Description = "name for the copy (default: Unity's \"Name (1)\" numbering)")]
        public string Name { get; set; }

        [CliParam(Description = "parent for the copy (default: same parent as the source)")]
        public string Parent { get; set; }
    }

    public sealed class GameObjectRenameParams : NodeTargetParams
    {
        [CliParam(Description = "new name", Required = true, AlternateKeys = "new_name")]
        public string Name { get; set; }
    }

    public sealed class GameObjectReparentParams : NodeTargetParams
    {
        [CliParam(Description = "new parent node path (empty string moves the node to the top level)",
            AlternateKeys = "new_parent")]
        public string Parent { get; set; }

        [CliParam(Description = "sibling index under the new parent (default: last)",
            AlternateKeys = "sibling_index")]
        public int? Index { get; set; }

        [CliParam(Description = "keep the world transform instead of the local one (default: true)",
            AlternateKeys = "world_position_stays")]
        public bool? KeepWorld { get; set; }
    }

    public sealed class GameObjectTransformParams : NodeTargetParams
    {
        [CliParam(Description = "position as [x,y,z], {x,y,z}, or a single number")]
        public JToken Position { get; set; }

        [CliParam(Description = "euler angles as [x,y,z] (or [x,y,z,w] for a raw quaternion)")]
        public JToken Rotation { get; set; }

        [CliParam(Description = "local scale as [x,y,z] or a single number")]
        public JToken Scale { get; set; }

        [CliParam(Description = "which space position/rotation are in (scale is always local)",
            AllowedValues = "local|world")]
        public string Space { get; set; } = "local";

        [CliParam(Description = "add to the current values instead of replacing them")]
        public bool Relative { get; set; }
    }

    public sealed class GameObjectActivateParams : NodeTargetParams
    {
        [CliParam(Description = "active state to set; omit to toggle")]
        public bool? Active { get; set; }
    }

    public sealed class GameObjectTagParams : NodeTargetParams
    {
        [CliParam(Description = "tag to assign (must already exist — create it with tag_add)", Required = true)]
        public string Tag { get; set; }

        [CliParam(Description = "also apply to every descendant")]
        public bool Recursive { get; set; }
    }

    public sealed class GameObjectLayerParams : NodeTargetParams
    {
        [CliParam(Description = "layer name or index", Required = true)]
        public string Layer { get; set; }

        [CliParam(Description = "also apply to every descendant")]
        public bool Recursive { get; set; }
    }

    public sealed class GameObjectTreeParams : NodeTargetParams
    {
        [CliParam(Description = "levels of children to walk; -1 for the whole subtree (default: 3)")]
        public int? Depth { get; set; }

        [CliParam(Description = "list component type names on each node (default: true)")]
        public bool? Components { get; set; }

        [CliParam(Description = "include inactive GameObjects (default: true)")]
        public bool? IncludeInactive { get; set; }
    }

    public sealed class GameObjectFindParams : EditTargetParams
    {
        [CliParam(Description = "name to match (substring unless exact=true)")]
        public string Name { get; set; }

        [CliParam(Description = "tag to match")]
        public string Tag { get; set; }

        [CliParam(Description = "layer name or index to match")]
        public string Layer { get; set; }

        [CliParam(Description = "only nodes carrying this component type")]
        public string Component { get; set; }

        [CliParam(Description = "only nodes whose hierarchy path contains this text",
            AlternateKeys = "path_contains,under")]
        public string Path { get; set; }

        [CliParam(Description = "require an exact name match")]
        public bool Exact { get; set; }

        [CliParam(Description = "include inactive GameObjects (default: true)")]
        public bool? IncludeInactive { get; set; }

        [CliParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }
}
