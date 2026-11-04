using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    // These replace the old multi-action `manage` command. `play`, `stop` and `refresh` already had
    // their own commands, so only pause, tags, layers and the active tool moved here.

    public sealed class PauseCommand : EditorActionCommand<PauseParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<PauseParams>(
            CommandNames.Pause,
            CommandHostScope.Editor,
            "Pause or resume play mode (omit 'paused' to toggle)");

        protected override Dictionary<string, object> Execute(PauseParams p) => EditorSettingsService.Pause(p);
    }

    public sealed class TagListCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.TagList,
            CommandHostScope.Editor,
            "List every tag defined in the project");

        protected override Dictionary<string, object> Execute() => EditorSettingsService.ListTags();
    }

    public sealed class TagAddCommand : EditorActionCommand<TagParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<TagParams>(
            CommandNames.TagAdd,
            CommandHostScope.Editor,
            "Define a new tag");

        protected override Dictionary<string, object> Execute(TagParams p) => EditorSettingsService.AddTag(p);
    }

    public sealed class TagRemoveCommand : EditorActionCommand<TagParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<TagParams>(
            CommandNames.TagRemove,
            CommandHostScope.Editor,
            "Remove a tag definition");

        protected override Dictionary<string, object> Execute(TagParams p) => EditorSettingsService.RemoveTag(p);
    }

    public sealed class LayerListCommand : EditorActionCommand
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor(
            CommandNames.LayerList,
            CommandHostScope.Editor,
            "List every layer defined in the project with its index");

        protected override Dictionary<string, object> Execute() => EditorSettingsService.ListLayers();
    }

    public sealed class LayerAddCommand : EditorActionCommand<LayerParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<LayerParams>(
            CommandNames.LayerAdd,
            CommandHostScope.Editor,
            "Define a new layer in the first free user slot");

        protected override Dictionary<string, object> Execute(LayerParams p) => EditorSettingsService.AddLayer(p);
    }

    public sealed class LayerRemoveCommand : EditorActionCommand<LayerParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<LayerParams>(
            CommandNames.LayerRemove,
            CommandHostScope.Editor,
            "Remove a user layer definition");

        protected override Dictionary<string, object> Execute(LayerParams p) =>
            EditorSettingsService.RemoveLayer(p);
    }

    public sealed class ToolSetCommand : EditorActionCommand<ToolSetParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ToolSetParams>(
            CommandNames.ToolSet,
            CommandHostScope.Editor,
            "Set the active Scene view transform tool");

        protected override Dictionary<string, object> Execute(ToolSetParams p) => EditorSettingsService.SetTool(p);
    }
}
