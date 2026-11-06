namespace Air.UcpAgent.Params
{
    public sealed class SelectionSetParams
    {
        [UcpParam(Description = "asset paths to select")]
        public string[] Assets { get; set; }

        [UcpParam(ParamKeys.Node, Description = "scene node paths to select, e.g. [\"Player/Body\"]")]
        public string[] Node { get; set; }

        [UcpParam(Description = "Unity instance ids to select")]
        public int[] Ids { get; set; }

        [UcpParam(ParamKeys.Scene, Description = "loaded scene the node paths belong to (default: active scene)")]
        public string Scene { get; set; }

        [UcpParam(Description = "add to the current selection instead of replacing it")]
        public bool Additive { get; set; }
    }

    public sealed class PauseParams
    {
        [UcpParam(Description = "paused state to set; omit to toggle")]
        public bool? Paused { get; set; }
    }

    public sealed class TagParams
    {
        [UcpParam(Description = "tag name", Required = true)]
        public string Tag { get; set; }
    }

    public sealed class LayerParams
    {
        [UcpParam(Description = "layer name", Required = true)]
        public string Layer { get; set; }
    }

    public sealed class ToolSetParams
    {
        [UcpParam(Description = "transform tool to activate", Required = true,
            AllowedValues = "view|move|rotate|scale|rect|transform|none")]
        public string Tool { get; set; }
    }
}
