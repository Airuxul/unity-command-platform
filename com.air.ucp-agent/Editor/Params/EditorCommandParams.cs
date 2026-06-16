namespace Air.UcpAgent.Params
{
    public sealed class ReserializeParams
    {
        [CliParam(Description = "comma-separated asset paths (omit for whole project)", AlternateKeys = "path")]
        public string Paths { get; set; }
    }

    public sealed class ManageEditorParams
    {
        [CliParam(Description = "operation to perform",
            AllowedValues = "play|stop|pause|refresh|set_active_tool|add_tag|remove_tag|add_layer|remove_layer",
            Required = true)]
        public string Action { get; set; }

        [CliParam(Description = "wait for completion", AlternateKeys = "wait_for_completion")]
        public bool Wait { get; set; }

        [CliParam("tool_name", Description = "tool name for set_active_tool")]
        public string ToolName { get; set; }

        [CliParam("tag_name", Description = "tag name for add/remove_tag")]
        public string TagName { get; set; }

        [CliParam("layer_name", Description = "layer name for add/remove_layer")]
        public string LayerName { get; set; }
    }

    public sealed class MenuParams
    {
        [CliParam("menu_path", Description = "Unity menu path, e.g. File/Save Project", Required = true,
            AlternateKeys = "path")]
        public string MenuPath { get; set; }
    }

    public sealed class ProfilerParams
    {
        [CliParam(Description = "profiler operation",
            AllowedValues = "enable|disable|status|clear|hierarchy",
            Required = true)]
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

        [CliParam(Description = "sort key", AllowedValues = "total|self")]
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
        [CliParam(Description = "view to capture", AllowedValues = "scene|game")]
        public string View { get; set; } = "scene";

        [CliParam("output_path", Description = "relative or absolute path")]
        public string OutputPath { get; set; }

        [CliParam(Description = "pixels")]
        public int? Width { get; set; }

        [CliParam(Description = "pixels")]
        public int? Height { get; set; }
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

        [CliParam(Description = "max entries", AlternateKeys = "count")]
        public int? Lines { get; set; }

        [CliParam(Description = "clear the console")]
        public bool Clear { get; set; }

        [CliParam(Description = "stacktrace verbosity", AllowedValues = "none|user|all")]
        public string Stacktrace { get; set; }
    }
}
