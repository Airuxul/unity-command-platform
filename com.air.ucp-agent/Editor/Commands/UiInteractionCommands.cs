using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using UnityEngine;

namespace Air.UcpAgent.Editor.Commands
{
    public sealed class UiClickCommand : EditorActionCommand<UiClickParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<UiClickParams>(
            CommandNames.UiClick,
            CommandHostScope.Editor,
            "Play mode only. Tap a UI node: pointer down, up and click go to the nearest handler at or above it. Fails on an inactive or non-interactable node.");

        protected override Dictionary<string, object> Execute(UiClickParams p) => UiInteractionService.Click(p);
    }

    public sealed class UiInputCommand : EditorActionCommand<UiInputParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<UiInputParams>(
            CommandNames.UiInput,
            CommandHostScope.Editor,
            "Play mode only. Set the text of an InputField or TMP_InputField as if typed, then end editing; submit=true presses Enter first.");

        protected override Dictionary<string, object> Execute(UiInputParams p) => UiInteractionService.Input(p);
    }

    public sealed class UiWaitCommand : CliCommand<UiWaitParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<UiWaitParams>(
            CommandNames.UiWait,
            CommandHostScope.Editor,
            "Play mode only. Wait until a node exists, is active, or shows a text, checked every frame; answers as soon as it holds. " +
            "Gives up shortly before --timeout with what the node looked like.");

        public override void Run(UiWaitParams p)
        {
            try
            {
                var condition = UiInteractionService.Condition(p);
                var payload = UiInteractionService.Check(condition, 0, out _);
                if (payload != null)
                {
                    CompleteSuccess(InvokeResult.Ok(Descriptor.Name, payload));
                    return;
                }

                Defer(InvokeCompletionCatalog.CompletionUiWait, JsonUtility.ToJson(condition));
            }
            catch (Exception ex)
            {
                CompleteFail(ex);
            }
        }
    }
}
