using Air.UcpAgent.Editor.Services.Objects;

namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Commands that only address a prefab asset on disk. The asset is the <c>path</c> — <c>prefab</c>
    /// is kept for the asset a content command edits.
    /// </summary>
    public class PrefabPathParams
    {
        [CliParam(ParamKeys.Path, Description = "prefab asset path, e.g. Assets/Prefabs/Player.prefab", Required = true)]
        public string Path { get; set; }
    }

    /// <summary>Commands that copy or move a prefab asset.</summary>
    public sealed class PrefabCopyParams : CopyParams
    {
    }

    public sealed class PrefabCreateParams : NodeTargetParams
    {
        [CliParam(ParamKeys.Path, Description = "prefab asset path to write, e.g. Assets/Prefabs/Player.prefab",
            Required = true, AlternateKeys = "output,to")]
        public string Path { get; set; }

        [CliParam(Description = "leave the source node linked to the new prefab as an instance (default: true)")]
        public bool? Connect { get; set; }
    }

    public sealed class PrefabCreateEmptyParams
    {
        [CliParam(ParamKeys.Path, Description = "prefab asset path to create", Required = true,
            AlternateKeys = "output")]
        public string Path { get; set; }

        [CliParam(Description = "root GameObject name (default: the file name)")]
        public string Name { get; set; }

        [CliParam(Description = "built-in shape for the root", AllowedValues = Primitives.Allowed)]
        public string Primitive { get; set; } = "empty";

        [CliParam(Description = "component type names to add to the root")]
        public string[] Components { get; set; }
    }

    /// <summary><c>path</c> is the base prefab, <c>target</c> the variant to write.</summary>
    public sealed class PrefabVariantParams : CopyParams
    {
    }

    public sealed class PrefabListParams : FolderQueryParams
    {
        [CliParam(Description = "include variants and model prefabs (default: true)")]
        public bool? IncludeVariants { get; set; }
    }

    public sealed class PrefabInfoParams : PrefabPathParams
    {
        [CliParam(Description = "also list asset dependencies")]
        public bool Dependencies { get; set; }
    }

    public sealed class PrefabTreeParams : PrefabPathParams
    {
        [CliParam(Description = "levels of children to walk; -1 for the whole tree (default: -1)")]
        public int? Depth { get; set; }

        [CliParam(Description = "list component type names on each node (default: true)")]
        public bool? Components { get; set; }
    }

    /// <summary>
    /// Instantiating is the one command with two prefab paths: <c>source</c> is what gets
    /// instantiated, and the inherited <c>prefab</c> is the asset it is instantiated
    /// <em>into</em> — which is how a nested prefab gets added without opening anything.
    /// </summary>
    public sealed class PrefabInstantiateParams : PlacementParams
    {
        [CliParam(ParamKeys.Source, Description = "prefab asset to instantiate", Required = true,
            AlternateKeys = "asset")]
        public string Source { get; set; }

        [CliParam(Description = "name for the instance (default: the prefab name)")]
        public string Name { get; set; }

        [CliParam(Description = "sibling index under the parent (default: last)",
            AlternateKeys = "sibling_index")]
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
        [CliParam(Description = "include Unity's default overrides (transform position, name, …)",
            AlternateKeys = "include_default_overrides")]
        public bool IncludeDefaults { get; set; }
    }

    /// <summary>
    /// Scopes an apply/revert. Nothing extra means the whole instance; <c>component</c> narrows it
    /// to one component; <c>component</c> + <c>property</c> narrows it to a single field.
    /// </summary>
    public sealed class PrefabOverrideParams : PrefabInstanceParams
    {
        [CliParam(Description = "limit to this component's overrides", AlternateKeys = "type")]
        public string Component { get; set; }

        [CliParam(Description = "which component when the node has several of this type (default: 0)")]
        public int? Index { get; set; }

        [CliParam(Description = "limit to this serialized property (requires 'component')")]
        public string Property { get; set; }
    }

    public sealed class PrefabUnpackParams : PrefabInstanceParams
    {
        [CliParam(Description = "also unpack nested prefab instances")]
        public bool Completely { get; set; }
    }

    public sealed class PrefabReplaceParams : PrefabInstanceParams
    {
        [CliParam(ParamKeys.Source, Description = "prefab asset to swap in", Required = true,
            AlternateKeys = "asset")]
        public string Source { get; set; }

        [CliParam(Description = "keep the instance's current transform values (default: true)")]
        public bool? KeepTransform { get; set; }
    }

    public sealed class PrefabStageOpenParams : PrefabPathParams
    {
        [CliParam(ParamKeys.Node, Description = "instance to open the prefab in the context of (optional)")]
        public string Node { get; set; }

        [CliParam(Description = "discard unsaved changes in the prefab that is already open")]
        public bool Force { get; set; }
    }

    public sealed class PrefabCloseParams
    {
        [CliParam(Description = "save the open prefab before leaving Prefab Mode (default: true)")]
        public bool? Save { get; set; }

        [CliParam(Description = "with save=false, discard unsaved changes instead of refusing")]
        public bool Force { get; set; }
    }
}
