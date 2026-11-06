namespace Air.UcpAgent.Params
{
    public sealed class ReserializeParams
    {
        [UcpParam(Description = "comma-separated asset paths")]
        public string Paths { get; set; }

        [UcpParam(Description = "reserialize the entire project (required when paths is empty)")]
        public bool All { get; set; }
    }

    public sealed class MenuParams
    {
        [UcpParam("menu_path", Description = "Unity menu path, e.g. File/Save Project", Required = true)]
        public string MenuPath { get; set; }
    }

    public sealed class ProfilerParams
    {
        [UcpParam(Description = "profiler operation (default: hierarchy)",
            AllowedValues = "enable|disable|status|clear|hierarchy")]
        public string Action { get; set; }

        [UcpParam(Description = "frame index (-1 = latest, hierarchy only)")]
        public int? Frame { get; set; }

        [UcpParam(Description = "start frame for range hierarchy")]
        public int? From { get; set; }

        [UcpParam(Description = "end frame for range hierarchy")]
        public int? To { get; set; }

        [UcpParam(Description = "frame count for averaged hierarchy")]
        public int? Frames { get; set; }

        [UcpParam(Description = "profiler thread index")]
        public int? Thread { get; set; }

        [UcpParam(Description = "filter root sample")]
        public string Root { get; set; }

        [UcpParam(Description = "parent sample id")]
        public int? Parent { get; set; }

        [UcpParam(Description = "minimum time threshold")]
        public float? Min { get; set; }

        [UcpParam(Description = "sort key", AllowedValues = "total|self|calls")]
        public string Sort { get; set; } = "total";

        [UcpParam(Description = "max depth")]
        public int? Depth { get; set; }

        [UcpParam(Description = "max children per node")]
        public int? Max { get; set; }
    }

    public sealed class ExecParams
    {
        [UcpParam(Description = "C# snippet (return value serialised)", Required = true)]
        public string Code { get; set; }

        [UcpParam("timeout_ms", Description = "compilation timeout")]
        public int? TimeoutMs { get; set; }

        [UcpParam(Description = "extra using directives")]
        public string[] Usings { get; set; }

        [UcpParam(Description = "path to csc compiler")]
        public string Csc { get; set; }

        [UcpParam(Description = "path to dotnet executable")]
        public string Dotnet { get; set; }
    }

    public sealed class ScreenshotParams
    {
        [UcpParam(Description = "what to capture; 'ui' is the only one that shows a ScreenSpaceOverlay canvas",
            AllowedValues = "scene|game|ui")]
        public string View { get; set; } = "scene";

        [UcpParam(ParamKeys.OutputPath, Description = "relative or absolute path")]
        public string OutputPath { get; set; }

        [UcpParam(Description = "pixels (view=ui defaults to the CanvasScaler reference resolution)")]
        public int? Width { get; set; }

        [UcpParam(Description = "pixels (view=ui defaults to the CanvasScaler reference resolution)")]
        public int? Height { get; set; }

        [UcpParam(ParamKeys.Source, Description = "view=ui: prefab asset to render a throwaway copy of (no side effects)")]
        public string Source { get; set; }

        [UcpParam(ParamKeys.Node, Description = "view=ui: node in an open scene whose canvas to render")]
        public string Node { get; set; }

        [UcpParam(ParamKeys.Scene, Description = "view=ui: loaded scene the node belongs to (default: active scene)")]
        public string Scene { get; set; }

        [UcpParam(Description = "view=ui: background as #RRGGBB, #RRGGBBAA or 'transparent' (default)")]
        public string Background { get; set; }
    }

    public sealed class BuildParams
    {
        [UcpParam(ParamKeys.OutputPath, Description = "the player to write, e.g. Builds/Win/Game.exe; relative to the project, not under Assets",
            Required = true)]
        public string OutputPath { get; set; }

        [UcpParam(Description = "BuildTarget name, e.g. StandaloneWindows64 or Android (default: the active one)")]
        public string Target { get; set; }

        [UcpParam(Description = "JSON array of scene asset paths (default: the enabled scenes in Build Settings)")]
        public string[] Scenes { get; set; }

        [UcpParam(Description = "development build")]
        public bool Development { get; set; }
    }

    public sealed class PackageListParams
    {
        [UcpParam(Description = "also list the packages that are only dependencies of others")]
        public bool Indirect { get; set; }
    }

    public sealed class PackageAddParams
    {
        [UcpParam(ParamKeys.Package, Description = "name, name@version, file:<path> or a git URL", Required = true)]
        public string Package { get; set; }
    }

    public sealed class PackageRemoveParams
    {
        [UcpParam(ParamKeys.Package, Description = "package name, e.g. com.unity.timeline", Required = true)]
        public string Package { get; set; }
    }

    public sealed class RefreshParams
    {
        [UcpParam(Description = "also trigger script compilation")]
        public bool Compile { get; set; }

        [UcpParam(Description = "allow refresh while entering play mode")]
        public bool Force { get; set; }
    }

    public sealed class PlayParams
    {
        [UcpParam("timeout_ms", Description = "override poll timeout (default: 20000)")]
        public int? TimeoutMs { get; set; }
    }

    public sealed class StopParams
    {
        [UcpParam("timeout_ms", Description = "override poll timeout (default: 20000)")]
        public int? TimeoutMs { get; set; }
    }

    public sealed class CompileParams
    {
        [UcpParam("timeout_ms", Description = "override poll timeout")]
        public int? TimeoutMs { get; set; }
    }

    public sealed class ConsoleParams
    {
        [UcpParam(Description = "log types to fetch", AllowedValues = "error|warning|log")]
        public string Type { get; set; } = "error,warning";

        [UcpParam(Description = "max entries, newest kept; identical entries are merged and counted")]
        public int? Lines { get; set; }

        [UcpParam(Description = "clear the console")]
        public bool Clear { get; set; }

        [UcpParam(Description = "stacktrace verbosity", AllowedValues = "none|user|all")]
        public string Stacktrace { get; set; }
    }

    public sealed class UiSnapshotParams
    {
        [UcpParam(Description = "only canvases and rows at or under this node path, as ui_snapshot prints it")]
        public string Root { get; set; }

        [UcpParam(Description = "max text / button / input rows in total; truncated says if more exist (default: 500)")]
        public int? Limit { get; set; }
    }

    public sealed class UiClickParams
    {
        [UcpParam(ParamKeys.Node, Description = "node path as ui_snapshot prints it; a child of a Button clicks the Button",
            Required = true)]
        public string Node { get; set; }
    }

    public sealed class UiInputParams
    {
        [UcpParam(ParamKeys.Node, Description = "InputField or TMP_InputField node, as ui_snapshot prints it", Required = true)]
        public string Node { get; set; }

        [UcpParam(Description = "replaces the field's text, \"\" clears it; onValueChanged and then onEndEdit fire (required)")]
        public string Text { get; set; }

        [UcpParam(Description = "also fire onSubmit before onEndEdit, as Enter does")]
        public bool Submit { get; set; }
    }

    public sealed class UiWaitParams
    {
        public const string Exists = "exists";
        public const string Active = "active";
        public const string ShowsText = "text";

        [UcpParam(ParamKeys.Node, Description = "node path as ui_snapshot prints it", Required = true)]
        public string Node { get; set; }

        [UcpParam(Description = "exists, active, or text: an active Text / TextMeshPro on or under node contains text " +
                                "(default: text when text is given, else active)",
            AllowedValues = Exists + "|" + Active + "|" + ShowsText)]
        public string Until { get; set; }

        [UcpParam(Description = "substring to wait for, case-sensitive")]
        public string Text { get; set; }
    }

}
