using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Params
{
    public sealed class GameObjectCreateParams : PlacementParams
    {
        [UcpParam(Description = "name of the new GameObject", Required = true)]
        public string Name { get; set; }

        [UcpParam(Description = "built-in shape to create", AllowedValues = Primitives.Allowed)]
        public string Primitive { get; set; } = "empty";

        [UcpParam(Description = "tag to assign")]
        public string Tag { get; set; }

        [UcpParam(Description = "layer name or index")]
        public string Layer { get; set; }

        [UcpParam(Description = "component type names to add, e.g. [\"Rigidbody\",\"BoxCollider\"]")]
        public string[] Components { get; set; }

        [UcpParam(Description = "initial active state (default: true)")]
        public bool? Active { get; set; }
    }

    /// <summary>Commands that only need to identify a node: delete, info.</summary>
    public sealed class NodeParams : NodeTargetParams
    {
    }

    public sealed class GameObjectDuplicateParams : NodeTargetParams
    {
        [UcpParam(Description = "name for the copy (default: Unity's \"Name (1)\" numbering)")]
        public string Name { get; set; }

        [UcpParam(ParamKeys.Parent, Description = "parent for the copy (default: same parent as the source)")]
        public string Parent { get; set; }
    }

    public sealed class GameObjectRenameParams : NodeTargetParams
    {
        [UcpParam(Description = "new name", Required = true)]
        public string Name { get; set; }
    }

    public sealed class GameObjectReparentParams : NodeTargetParams
    {
        [UcpParam(ParamKeys.Parent, Description = "new parent node path (empty string moves the node to the top level)")]
        public string Parent { get; set; }

        [UcpParam(Description = "sibling index under the new parent (default: last)")]
        public int? Index { get; set; }

        [UcpParam(Description = "keep the world transform instead of the local one (default: true)")]
        public bool? KeepWorld { get; set; }
    }

    public sealed class GameObjectTransformParams : NodeTargetParams
    {
        [UcpParam(Description = "position as [x,y,z], {x,y,z}, or a single number")]
        public JToken Position { get; set; }

        [UcpParam(Description = "euler angles as [x,y,z] (or [x,y,z,w] for a raw quaternion)")]
        public JToken Rotation { get; set; }

        [UcpParam(Description = "local scale as [x,y,z] or a single number")]
        public JToken Scale { get; set; }

        [UcpParam(Description = "which space position/rotation are in (scale is always local)",
            AllowedValues = "local|world")]
        public string Space { get; set; } = "local";

        [UcpParam(Description = "add to the current values instead of replacing them")]
        public bool Relative { get; set; }
    }

    public sealed class GameObjectActivateParams : NodeTargetParams
    {
        [UcpParam(Description = "active state to set; omit to toggle")]
        public bool? Active { get; set; }
    }

    public sealed class GameObjectTagParams : NodeTargetParams
    {
        [UcpParam(Description = "tag to assign (must already exist — create it with " + CommandNames.TagAdd + ")",
            Required = true)]
        public string Tag { get; set; }

        [UcpParam(Description = "also apply to every descendant")]
        public bool Recursive { get; set; }
    }

    public sealed class GameObjectLayerParams : NodeTargetParams
    {
        [UcpParam(Description = "layer name or index", Required = true)]
        public string Layer { get; set; }

        [UcpParam(Description = "also apply to every descendant")]
        public bool Recursive { get; set; }
    }

    public sealed class GameObjectTreeParams : NodeTargetParams
    {
        [UcpParam(Description = "levels of children to walk; -1 for the whole subtree (default: 3)")]
        public int? Depth { get; set; }

        [UcpParam(Description = "list component type names on each node (default: true)")]
        public bool? Components { get; set; }

        [UcpParam(Description = "include inactive GameObjects (default: true)")]
        public bool? IncludeInactive { get; set; }

        [UcpParam(Description = "stop after this many nodes and report truncated (default: 500)")]
        public int? Limit { get; set; }

        [UcpParam(Description = "json: nested objects; compact: one line per node, 'path [Comp,..] (inactive)'",
            AllowedValues = TreeShape.Formats)]
        public string Format { get; set; } = TreeShape.Json;

        [UcpParam(Description = "json only: comma-separated fields to keep on each node (default: all)",
            AllowedValues = TreeShape.Fields)]
        public string Fields { get; set; }
    }

    public sealed class GameObjectFindParams : EditTargetParams
    {
        [UcpParam(Description = "name to match (substring unless exact=true)")]
        public string Name { get; set; }

        [UcpParam(Description = "tag to match")]
        public string Tag { get; set; }

        [UcpParam(Description = "layer name or index to match")]
        public string Layer { get; set; }

        [UcpParam(Description = "only nodes carrying this component type")]
        public string Component { get; set; }

        [UcpParam(Description = "only nodes whose hierarchy path contains this text")]
        public string Path { get; set; }

        [UcpParam(Description = "require an exact name match")]
        public bool Exact { get; set; }

        [UcpParam(Description = "include inactive GameObjects (default: true)")]
        public bool? IncludeInactive { get; set; }

        [UcpParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }
}
