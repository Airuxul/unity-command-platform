namespace Air.UcpAgent.Params
{
    public sealed class ReserializeParams
    {
        [CliParam(Description = "comma-separated asset paths", AlternateKeys = "path")]
        public string Paths { get; set; }

        [CliParam(Description = "reserialize the entire project (required when paths is empty)")]
        public bool All { get; set; }
    }

    public sealed class MenuParams
    {
        [CliParam("menu_path", Description = "Unity menu path, e.g. File/Save Project", Required = true,
            AlternateKeys = "path")]
        public string MenuPath { get; set; }
    }

    public sealed class ProfilerParams
    {
        [CliParam(Description = "profiler operation (default: hierarchy)",
            AllowedValues = "enable|disable|status|clear|hierarchy")]
        public string Action { get; set; }

        [CliParam(Description = "frame index (-1 = latest, hierarchy only)")]
        public int? Frame { get; set; }

        [CliParam(Description = "start frame for range hierarchy")]
        public int? From { get; set; }

        [CliParam(Description = "end frame for range hierarchy")]
        public int? To { get; set; }

        [CliParam(Description = "frame count for averaged hierarchy")]
        public int? Frames { get; set; }

        [CliParam(Description = "profiler thread index")]
        public int? Thread { get; set; }

        [CliParam(Description = "filter root sample")]
        public string Root { get; set; }

        [CliParam(Description = "parent sample id")]
        public int? Parent { get; set; }

        [CliParam(Description = "minimum time threshold")]
        public float? Min { get; set; }

        [CliParam(Description = "sort key", AllowedValues = "total|self|calls")]
        public string Sort { get; set; } = "total";

        [CliParam(Description = "max depth")]
        public int? Depth { get; set; }

        [CliParam(Description = "max children per node")]
        public int? Max { get; set; }
    }

    public sealed class ExecParams
    {
        [CliParam(Description = "C# snippet (return value serialised)", Required = true)]
        public string Code { get; set; }

        [CliParam("timeout_ms", Description = "compilation timeout")]
        public int? TimeoutMs { get; set; }

        [CliParam(Description = "extra using directives")]
        public string[] Usings { get; set; }

        [CliParam(Description = "path to csc compiler")]
        public string Csc { get; set; }

        [CliParam(Description = "path to dotnet executable")]
        public string Dotnet { get; set; }
    }

    public sealed class ScreenshotParams
    {
        [CliParam(Description = "what to capture; 'ui' is the only one that shows a ScreenSpaceOverlay canvas",
            AllowedValues = "scene|game|ui")]
        public string View { get; set; } = "scene";

        [CliParam("output_path", Description = "relative or absolute path")]
        public string OutputPath { get; set; }

        [CliParam(Description = "pixels (view=ui defaults to the CanvasScaler reference resolution)")]
        public int? Width { get; set; }

        [CliParam(Description = "pixels (view=ui defaults to the CanvasScaler reference resolution)")]
        public int? Height { get; set; }

        [CliParam(ParamKeys.Source, Description = "view=ui: prefab asset to render a throwaway copy of (no side effects)")]
        public string Source { get; set; }

        [CliParam(ParamKeys.Node, Description = "view=ui: node in an open scene whose canvas to render")]
        public string Node { get; set; }

        [CliParam(ParamKeys.Scene, Description = "view=ui: loaded scene the node belongs to (default: active scene)")]
        public string Scene { get; set; }

        [CliParam(Description = "view=ui: background as #RRGGBB, #RRGGBBAA or 'transparent' (default)")]
        public string Background { get; set; }
    }

    public sealed class RefreshParams
    {
        [CliParam(Description = "also trigger script compilation")]
        public bool Compile { get; set; }

        [CliParam(Description = "allow refresh while entering play mode")]
        public bool Force { get; set; }
    }

    public sealed class PlayParams
    {
        [CliParam("timeout_ms", Description = "override poll timeout (default: 20000)")]
        public int? TimeoutMs { get; set; }
    }

    public sealed class StopParams
    {
        [CliParam("timeout_ms", Description = "override poll timeout (default: 20000)")]
        public int? TimeoutMs { get; set; }
    }

    public sealed class CompileParams
    {
        [CliParam("timeout_ms", Description = "override poll timeout")]
        public int? TimeoutMs { get; set; }
    }

    public sealed class ConsoleParams
    {
        [CliParam(Description = "log types to fetch", AllowedValues = "error|warning|log")]
        public string Type { get; set; } = "error,warning";

        [CliParam(Description = "max entries, newest kept; identical entries are merged and counted", AlternateKeys = "count")]
        public int? Lines { get; set; }

        [CliParam(Description = "clear the console")]
        public bool Clear { get; set; }

        [CliParam(Description = "stacktrace verbosity", AllowedValues = "none|user|all")]
        public string Stacktrace { get; set; }
    }

    public sealed class UiSnapshotParams
    {
        [CliParam(Description = "only canvases and rows at or under this node path, as ui_snapshot prints it")]
        public string Root { get; set; }

        [CliParam(Description = "max text / button / input rows in total; truncated says if more exist (default: 500)")]
        public int? Limit { get; set; }
    }

    public sealed class HelpParams
    {
        public const string Json = "json";
        public const string Markdown = "md";

        [CliParam(Description = "one command by name or alias: its card, with an example call and the error codes")]
        public string Command { get; set; }

        [CliParam(Description = "md: the cards as markdown in data.markdown", AllowedValues = Json + "|" + Markdown)]
        public string Format { get; set; } = Json;
    }
}
