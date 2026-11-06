using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Completion;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using UnityEngine;

namespace Air.UcpAgent.Editor.Commands
{
    [UcpCommand]
    public static class UiClickCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<UiClickParams>(
            CommandNames.UiClick,
            CommandHostScope.Editor,
            "Play mode only. Tap a UI node: pointer down, up and click go to the nearest handler at or above it. Fails on an inactive or non-interactable node.");

        public static object Execute(UiClickParams p, CommandContext ctx) => UiInteractionService.Click(p);
    }

    [UcpCommand]
    public static class UiInputCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<UiInputParams>(
            CommandNames.UiInput,
            CommandHostScope.Editor,
            "Play mode only. Set the text of an InputField or TMP_InputField as if typed, then end editing; submit=true presses Enter first.");

        public static object Execute(UiInputParams p, CommandContext ctx) => UiInteractionService.Input(p);
    }

    [UcpCommand]
    public static class UiWaitCommand
    {
        public static readonly InvokeDescriptor Descriptor = new InvokeDescriptor<UiWaitParams>(
            CommandNames.UiWait,
            CommandHostScope.Editor,
            "Play mode only. Wait until a node exists, is active, or shows a text, checked every frame; answers as soon as it holds. " +
            "Gives up shortly before --timeout with what the node looked like.");

        public static object Execute(UiWaitParams p, CommandContext ctx)
        {
            var condition = UiInteractionService.Condition(p);
            var payload = UiInteractionService.Check(condition, 0, out _);
            if (payload != null)
                return payload;

            ctx.Defer(typeof(UiWaitPolicy), UcpJson.Serialize(condition));
            return null;
        }
    }
}
