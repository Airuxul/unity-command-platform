namespace Air.UcpAgent.Params
{
    public sealed class SceneOpenParams
    {
        [UcpParam(ParamKeys.Path, Description = "scene asset path, e.g. Assets/Scenes/Main.unity", Required = true)]
        public string Path { get; set; }

        [UcpParam(Description = "how to load it", AllowedValues = "single|additive|additive_without_loading")]
        public string Mode { get; set; } = "single";

        [UcpParam(Description = "discard unsaved changes in the currently open scenes")]
        public bool Force { get; set; }
    }

    public sealed class SceneNewParams
    {
        [UcpParam(Description = "what the new scene starts with",
            AllowedValues = "empty|default")]
        public string Setup { get; set; } = "default";

        [UcpParam(Description = "replace the open scenes or add to them", AllowedValues = "single|additive")]
        public string Mode { get; set; } = "single";

        [UcpParam(ParamKeys.Path, Description = "save the new scene to this path straight away")]
        public string Path { get; set; }

        [UcpParam(Description = "discard unsaved changes in the currently open scenes")]
        public bool Force { get; set; }
    }

    public sealed class SceneSaveParams
    {
        [UcpParam(ParamKeys.Scene, Description = "loaded scene name or path (default: active scene)")]
        public string Scene { get; set; }

        [UcpParam(Description = "save every open scene instead of just one")]
        public bool All { get; set; }
    }

    public sealed class SceneSaveAsParams
    {
        [UcpParam(ParamKeys.Path, Description = "path to save to, e.g. Assets/Scenes/Copy.unity", Required = true)]
        public string Path { get; set; }

        [UcpParam(ParamKeys.Scene, Description = "loaded scene to save (default: active scene)")]
        public string Scene { get; set; }
    }

    public sealed class SceneCloseParams
    {
        [UcpParam(ParamKeys.Scene, Description = "loaded scene name or path", Required = true)]
        public string Scene { get; set; }

        [UcpParam(Description = "also remove it from the hierarchy window (default: true)")]
        public bool? Remove { get; set; }

        [UcpParam(Description = "discard unsaved changes in that scene")]
        public bool Force { get; set; }
    }

    public sealed class SceneListParams : FolderQueryParams
    {
    }
}
