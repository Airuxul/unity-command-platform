using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Scenes;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class SceneOpenCommand : EditorActionCommand<SceneOpenParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneOpenParams>(
            CommandNames.SceneOpen,
            CommandHostScope.Editor,
            "Open a scene asset (single or additive)");

        protected override Dictionary<string, object> Execute(SceneOpenParams p) => SceneService.Open(p);
    }

    public sealed class SceneNewCommand : EditorActionCommand<SceneNewParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneNewParams>(
            CommandNames.SceneNew,
            CommandHostScope.Editor,
            "Create a new scene, optionally saving it straight to a path");

        protected override Dictionary<string, object> Execute(SceneNewParams p) => SceneService.New(p);
    }

    public sealed class SceneSaveCommand : EditorActionCommand<SceneSaveParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneSaveParams>(
            CommandNames.SceneSave,
            CommandHostScope.Editor,
            "Save one open scene, or all of them with all=true");

        protected override Dictionary<string, object> Execute(SceneSaveParams p) => SceneService.Save(p);
    }

    public sealed class SceneSaveAsCommand : EditorActionCommand<SceneSaveAsParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneSaveAsParams>(
            CommandNames.SceneSaveAs,
            CommandHostScope.Editor,
            "Save an open scene to a new path");

        protected override Dictionary<string, object> Execute(SceneSaveAsParams p) => SceneService.SaveAs(p);
    }

    public sealed class SceneCloseCommand : EditorActionCommand<SceneCloseParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneCloseParams>(
            CommandNames.SceneClose,
            CommandHostScope.Editor,
            "Close an additively loaded scene");

        protected override Dictionary<string, object> Execute(SceneCloseParams p) => SceneService.Close(p);
    }

    public sealed class SceneListCommand : EditorActionCommand<SceneListParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<SceneListParams>(
            CommandNames.SceneList,
            CommandHostScope.Editor,
            "List scene assets in the project and which are in the build settings");

        protected override Dictionary<string, object> Execute(SceneListParams p) => SceneService.List(p);
    }

    public sealed class SceneInfoCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.SceneInfo,
            CommandHostScope.Editor,
            "Active scene and every loaded scene, with their dirty state");

        protected override Dictionary<string, object> Execute() => SceneService.Info();
    }
}
