using Air.UcpAgent.Cli;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Editor.Commands
{
    // These replace the old multi-action `manage` command. `play`, `stop` and `refresh` already had
    // their own commands, so only pause, tags, layers and the active tool moved here.

    [UcpCommand]
    public static class PauseCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<PauseParams>(
            CommandNames.Pause,
            CommandHostScope.Editor,
            "Pause or resume play mode (omit 'paused' to toggle)");

        public static object Execute(PauseParams p, CommandContext ctx) => EditorSettingsService.Pause(p);
    }

    [UcpCommand]
    public static class TagListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.TagList,
            CommandHostScope.Editor,
            "List every tag defined in the project");

        public static object Execute(CommandContext ctx) => EditorSettingsService.ListTags();
    }

    [UcpCommand]
    public static class TagAddCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<TagParams>(
            CommandNames.TagAdd,
            CommandHostScope.Editor,
            "Define a new tag");

        public static object Execute(TagParams p, CommandContext ctx) => EditorSettingsService.AddTag(p);
    }

    [UcpCommand]
    public static class TagRemoveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<TagParams>(
            CommandNames.TagRemove,
            CommandHostScope.Editor,
            "Remove a tag definition");

        public static object Execute(TagParams p, CommandContext ctx) => EditorSettingsService.RemoveTag(p);
    }

    [UcpCommand]
    public static class LayerListCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor(
            CommandNames.LayerList,
            CommandHostScope.Editor,
            "List every layer defined in the project with its index");

        public static object Execute(CommandContext ctx) => EditorSettingsService.ListLayers();
    }

    [UcpCommand]
    public static class LayerAddCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<LayerParams>(
            CommandNames.LayerAdd,
            CommandHostScope.Editor,
            "Define a new layer in the first free user slot");

        public static object Execute(LayerParams p, CommandContext ctx) => EditorSettingsService.AddLayer(p);
    }

    [UcpCommand]
    public static class LayerRemoveCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<LayerParams>(
            CommandNames.LayerRemove,
            CommandHostScope.Editor,
            "Remove a user layer definition");

        public static object Execute(LayerParams p, CommandContext ctx) =>
            EditorSettingsService.RemoveLayer(p);
    }

    [UcpCommand]
    public static class ToolSetCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<ToolSetParams>(
            CommandNames.ToolSet,
            CommandHostScope.Editor,
            "Set the active Scene view transform tool");

        public static object Execute(ToolSetParams p, CommandContext ctx) => EditorSettingsService.SetTool(p);
    }
}
