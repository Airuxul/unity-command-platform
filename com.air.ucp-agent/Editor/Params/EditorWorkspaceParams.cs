namespace Air.UcpAgent.Params
{
    public sealed class SelectionSetParams
    {
        [CliParam(Description = "asset paths to select")]
        public string[] Assets { get; set; }

        [CliParam(ParamKeys.Node, Description = "scene node paths to select, e.g. [\"Player/Body\"]",
            AlternateKeys = "gameobjects,nodes")]
        public string[] Node { get; set; }

        [CliParam(Description = "Unity instance ids to select", AlternateKeys = "instance_ids")]
        public int[] Ids { get; set; }

        [CliParam(ParamKeys.Scene, Description = "loaded scene the node paths belong to (default: active scene)")]
        public string Scene { get; set; }

        [CliParam(Description = "add to the current selection instead of replacing it")]
        public bool Additive { get; set; }
    }

    public sealed class PauseParams
    {
        [CliParam(Description = "paused state to set; omit to toggle")]
        public bool? Paused { get; set; }
    }

    public sealed class TagParams
    {
        [CliParam(Description = "tag name", Required = true, AlternateKeys = "tag_name,name")]
        public string Tag { get; set; }
    }

    public sealed class LayerParams
    {
        [CliParam(Description = "layer name", Required = true, AlternateKeys = "layer_name,name")]
        public string Layer { get; set; }
    }

    public sealed class ToolSetParams
    {
        [CliParam(Description = "transform tool to activate", Required = true,
            AllowedValues = "view|move|rotate|scale|rect|transform|none",
            AlternateKeys = "tool_name,name")]
        public string Tool { get; set; }
    }
}
