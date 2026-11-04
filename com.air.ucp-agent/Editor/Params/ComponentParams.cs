using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Params
{
    /// <summary>Identifies one component on one node. Index disambiguates repeated types.</summary>
    public abstract class ComponentTargetParams : NodeTargetParams
    {
        [CliParam(Description = "component type, e.g. Rigidbody, UnityEngine.BoxCollider, TMPro.TextMeshProUGUI",
            Required = true, AlternateKeys = "type,component_type")]
        public string Component { get; set; }

        [CliParam(Description = "which one when the node has several of this type (default: 0)")]
        public int? Index { get; set; }
    }

    public sealed class ComponentAddParams : NodeTargetParams
    {
        [CliParam(Description = "component type to add", Required = true,
            AlternateKeys = "type,component_type")]
        public string Component { get; set; }

        [CliParam(Description = "properties to set on the new component, e.g. {\"mass\":5,\"useGravity\":false}")]
        public JObject Properties { get; set; }
    }

    public sealed class ComponentRemoveParams : ComponentTargetParams
    {
    }

    public sealed class ComponentListParams : NodeTargetParams
    {
    }

    public sealed class ComponentGetParams : ComponentTargetParams
    {
        [CliParam(Description = "single property to read; omit to read them all")]
        public string Property { get; set; }

        [CliParam(Description = "nesting levels of structs/arrays to expand (default: 2)")]
        public int? Depth { get; set; }

        [CliParam(Description = "array elements kept before the tail is summarised (default: 64)",
            AlternateKeys = "max_array")]
        public int? MaxArray { get; set; }

        [CliParam(Description = "include Unity bookkeeping fields such as m_ObjectHideFlags",
            AlternateKeys = "include_internal")]
        public bool Internal { get; set; }
    }

    public sealed class ComponentSetParams : ComponentTargetParams
    {
        [CliParam(Description = "properties to set; object refs accept an asset path, "
            + "{\"guid\":..}, {\"builtin\":\"LegacyRuntime.ttf\"}, {\"node\":..,\"component\":..} or an instance id")]
        public JObject Properties { get; set; }

        [CliParam(Description = "single property name, paired with 'value'")]
        public string Property { get; set; }

        [CliParam(Description = "value for the single property named by 'property'")]
        public JToken Value { get; set; }
    }
}
