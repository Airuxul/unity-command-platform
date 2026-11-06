using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Params
{
    /// <summary>Identifies one component on one node. Index disambiguates repeated types.</summary>
    public abstract class ComponentTargetParams : NodeTargetParams
    {
        [UcpParam(Description = "component type, e.g. Rigidbody, UnityEngine.BoxCollider, TMPro.TextMeshProUGUI",
            Required = true)]
        public string Component { get; set; }

        [UcpParam(Description = "which one when the node has several of this type (default: 0)")]
        public int? Index { get; set; }
    }

    public sealed class ComponentAddParams : NodeTargetParams
    {
        [UcpParam(Description = "component type to add", Required = true)]
        public string Component { get; set; }

        [UcpParam(Description = "properties to set on the new component, e.g. {\"mass\":5,\"useGravity\":false}")]
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
        [UcpParam(Description = "single property to read; omit to read them all")]
        public string Property { get; set; }

        [UcpParam(Description = "nesting levels of structs/arrays to expand (default: 2)")]
        public int? Depth { get; set; }

        [UcpParam(Description = "array elements kept before the tail is summarised (default: 64)")]
        public int? MaxArray { get; set; }

        [UcpParam(Description = "include Unity bookkeeping fields such as m_ObjectHideFlags")]
        public bool Internal { get; set; }
    }

    public sealed class ComponentSetParams : ComponentTargetParams
    {
        [UcpParam(Description = "properties to set; object refs accept an asset path, "
            + "{\"guid\":..}, \"builtin:LegacyRuntime.ttf\", {\"node\":..,\"component\":..} or {\"instance_id\":..}")]
        public JObject Properties { get; set; }

        [UcpParam(Description = "single property name, paired with 'value'")]
        public string Property { get; set; }

        [UcpParam(Description = "value for the single property named by 'property'")]
        public JToken Value { get; set; }
    }
}
