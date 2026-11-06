using Air.UcpAgent.Editor.Services.Objects;

namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Commands that only address a prefab asset on disk. The asset is the <c>path</c> — <c>prefab</c>
    /// is kept for the asset a content command edits.
    /// </summary>
    public class PrefabPathParams
    {
        [UcpParam(ParamKeys.Path, Description = "prefab asset path, e.g. Assets/Prefabs/Player.prefab", Required = true)]
        public string Path { get; set; }
    }

    /// <summary>Commands that copy or move a prefab asset.</summary>
    public sealed class PrefabCopyParams : CopyParams
    {
    }

    public sealed class PrefabCreateParams : NodeTargetParams
    {
        [UcpParam(ParamKeys.Path, Description = "prefab asset path to write, e.g. Assets/Prefabs/Player.prefab",
            Required = true)]
        public string Path { get; set; }

        [UcpParam(Description = "leave the source node linked to the new prefab as an instance (default: true)")]
        public bool? Connect { get; set; }
    }

    public sealed class PrefabCreateEmptyParams
    {
        [UcpParam(ParamKeys.Path, Description = "prefab asset path to create", Required = true)]
        public string Path { get; set; }

        [UcpParam(Description = "root GameObject name (default: the file name)")]
        public string Name { get; set; }

        [UcpParam(Description = "built-in shape for the root", AllowedValues = Primitives.Allowed)]
        public string Primitive { get; set; } = "empty";

        [UcpParam(Description = "component type names to add to the root")]
        public string[] Components { get; set; }
    }

    /// <summary><c>path</c> is the base prefab, <c>target</c> the variant to write.</summary>
    public sealed class PrefabVariantParams : CopyParams
    {
    }

    public sealed class PrefabListParams : FolderQueryParams
    {
        [UcpParam(Description = "include variants and model prefabs (default: true)")]
        public bool? IncludeVariants { get; set; }
    }

    public sealed class PrefabInfoParams : PrefabPathParams
    {
        [UcpParam(Description = "also list asset dependencies")]
        public bool Dependencies { get; set; }
    }

    public sealed class PrefabTreeParams : PrefabPathParams
    {
        [UcpParam(Description = "levels of children to walk; -1 for the whole tree (default: -1)")]
        public int? Depth { get; set; }

        [UcpParam(Description = "list component type names on each node (default: true)")]
        public bool? Components { get; set; }

        [UcpParam(ParamKeys.Node, Description = "only the subtree under this node path (default: the root)")]
        public string Node { get; set; }

        [UcpParam(Description = "stop after this many nodes and report truncated (default: 500)")]
        public int? Limit { get; set; }

        [UcpParam(Description = "json: nested objects; compact: one line per node, 'path [Comp,..] (inactive)'",
            AllowedValues = TreeShape.Formats)]
        public string Format { get; set; } = TreeShape.Json;

        [UcpParam(Description = "json only: comma-separated fields to keep on each node (default: all)",
            AllowedValues = TreeShape.Fields)]
        public string Fields { get; set; }
    }

    /// <summary>
    /// Instantiating is the one command with two prefab paths: <c>source</c> is what gets
    /// instantiated, and the inherited <c>prefab</c> is the asset it is instantiated
    /// <em>into</em> — which is how a nested prefab gets added without opening anything.
    /// </summary>
    public sealed class PrefabInstantiateParams : PlacementParams
    {
        [UcpParam(ParamKeys.Source, Description = "prefab asset to instantiate", Required = true)]
        public string Source { get; set; }

        [UcpParam(Description = "name for the instance (default: the prefab name)")]
        public string Name { get; set; }

        [UcpParam(Description = "sibling index under the parent (default: last)")]
        public int? Index { get; set; }
    }

    /// <summary>
    /// Addresses a prefab instance. Inherits the edit target, so it also reaches nested prefab
    /// instances living inside another prefab asset.
    /// </summary>
    public class PrefabInstanceParams : NodeTargetParams
    {
    }

    public sealed class PrefabOverridesParams : PrefabInstanceParams
    {
        [UcpParam(Description = "include Unity's default overrides (transform position, name, …)")]
        public bool IncludeDefaults { get; set; }
    }

    /// <summary>
    /// Scopes an apply/revert. Nothing extra means the whole instance; <c>component</c> narrows it
    /// to one component; <c>component</c> + <c>property</c> narrows it to a single field.
    /// </summary>
    public sealed class PrefabOverrideParams : PrefabInstanceParams
    {
        [UcpParam(Description = "limit to this component's overrides")]
        public string Component { get; set; }

        [UcpParam(Description = "which component when the node has several of this type (default: 0)")]
        public int? Index { get; set; }

        [UcpParam(Description = "limit to this serialized property (requires 'component')")]
        public string Property { get; set; }
    }

    public sealed class PrefabUnpackParams : PrefabInstanceParams
    {
        [UcpParam(Description = "also unpack nested prefab instances")]
        public bool Completely { get; set; }
    }

    public sealed class PrefabReplaceParams : PrefabInstanceParams
    {
        [UcpParam(ParamKeys.Source, Description = "prefab asset to swap in", Required = true)]
        public string Source { get; set; }

        [UcpParam(Description = "keep the instance's current transform values (default: true)")]
        public bool? KeepTransform { get; set; }
    }

    public sealed class PrefabStageOpenParams : PrefabPathParams
    {
        [UcpParam(ParamKeys.Node, Description = "instance to open the prefab in the context of (optional)")]
        public string Node { get; set; }

        [UcpParam(Description = "discard unsaved changes in the prefab that is already open")]
        public bool Force { get; set; }
    }

    public sealed class PrefabCloseParams
    {
        [UcpParam(Description = "save the open prefab before leaving Prefab Mode (default: true)")]
        public bool? Save { get; set; }

        [UcpParam(Description = "with save=false, discard unsaved changes instead of refusing")]
        public bool Force { get; set; }
    }
}
