using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Scenes;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class SceneOpenCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SceneOpenParams>(
            CommandNames.SceneOpen,
            CommandHostScope.Editor,
            "Open a scene asset (single or additive)");

        public static object Execute(SceneOpenParams p, CommandContext ctx) => SceneService.Open(p);
    }

    [UcpCommand]
    public static class SceneNewCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SceneNewParams>(
            CommandNames.SceneNew,
            CommandHostScope.Editor,
            "Create a new scene, optionally saving it straight to a path");

        public static object Execute(SceneNewParams p, CommandContext ctx) => SceneService.New(p);
    }

    [UcpCommand]
    public static class SceneSaveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SceneSaveParams>(
            CommandNames.SceneSave,
            CommandHostScope.Editor,
            "Save one open scene, or all of them with all=true");

        public static object Execute(SceneSaveParams p, CommandContext ctx) => SceneService.Save(p);
    }

    [UcpCommand]
    public static class SceneSaveAsCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SceneSaveAsParams>(
            CommandNames.SceneSaveAs,
            CommandHostScope.Editor,
            "Save an open scene to a new path");

        public static object Execute(SceneSaveAsParams p, CommandContext ctx) => SceneService.SaveAs(p);
    }

    [UcpCommand]
    public static class SceneCloseCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SceneCloseParams>(
            CommandNames.SceneClose,
            CommandHostScope.Editor,
            "Close an additively loaded scene");

        public static object Execute(SceneCloseParams p, CommandContext ctx) => SceneService.Close(p);
    }

    [UcpCommand]
    public static class SceneListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<SceneListParams>(
            CommandNames.SceneList,
            CommandHostScope.Editor,
            "List scene assets in the project and which are in the build settings");

        public static object Execute(SceneListParams p, CommandContext ctx) => SceneService.List(p);
    }

    [UcpCommand]
    public static class SceneInfoCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.SceneInfo,
            CommandHostScope.Editor,
            "Active scene and every loaded scene, with their dirty state");

        public static object Execute(CommandContext ctx) => SceneService.Info();
    }
}
