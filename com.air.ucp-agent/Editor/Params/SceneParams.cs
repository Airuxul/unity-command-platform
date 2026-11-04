namespace Air.UcpAgent.Params
{
    public sealed class SceneOpenParams
    {
        [CliParam(Description = "scene asset path, e.g. Assets/Scenes/Main.unity", Required = true,
            AlternateKeys = "scene,scene_path")]
        public string Path { get; set; }

        [CliParam(Description = "how to load it", AllowedValues = "single|additive|additive_without_loading")]
        public string Mode { get; set; } = "single";

        [CliParam(Description = "discard unsaved changes in the currently open scenes")]
        public bool Force { get; set; }
    }

    public sealed class SceneNewParams
    {
        [CliParam(Description = "what the new scene starts with",
            AllowedValues = "empty|default")]
        public string Setup { get; set; } = "default";

        [CliParam(Description = "replace the open scenes or add to them", AllowedValues = "single|additive")]
        public string Mode { get; set; } = "single";

        [CliParam(Description = "save the new scene to this path straight away")]
        public string Path { get; set; }

        [CliParam(Description = "discard unsaved changes in the currently open scenes")]
        public bool Force { get; set; }
    }

    public sealed class SceneSaveParams
    {
        [CliParam(Description = "loaded scene name or path (default: active scene)",
            AlternateKeys = "scene_name")]
        public string Scene { get; set; }

        [CliParam(Description = "save every open scene instead of just one")]
        public bool All { get; set; }
    }

    public sealed class SceneSaveAsParams
    {
        [CliParam(Description = "path to save to, e.g. Assets/Scenes/Copy.unity", Required = true,
            AlternateKeys = "scene_path,output,to")]
        public string Path { get; set; }

        [CliParam(Description = "loaded scene to save (default: active scene)",
            AlternateKeys = "scene_name")]
        public string Scene { get; set; }
    }

    public sealed class SceneCloseParams
    {
        [CliParam(Description = "loaded scene name or path", Required = true, AlternateKeys = "scene_name")]
        public string Scene { get; set; }

        [CliParam(Description = "also remove it from the hierarchy window (default: true)")]
        public bool? Remove { get; set; }

        [CliParam(Description = "discard unsaved changes in that scene")]
        public bool Force { get; set; }
    }

    public sealed class SceneListParams
    {
        [CliParam(Description = "folder to search under (default: Assets)")]
        public string Folder { get; set; }

        [CliParam(Description = "name substring filter")]
        public string Name { get; set; }

        [CliParam(Description = "max results (default: 200)")]
        public int? Limit { get; set; }
    }
}
