using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>Drives the running game's UI the way a player would: click, type, and wait for what follows.</summary>
    public static class UiInteractionService
    {
        // TextMeshPro is an optional package, so it is looked up by name rather than referenced.
        const string TmpText = "TMPro.TMP_Text";
        const string TmpInputField = "TMPro.TMP_InputField";

        public static Dictionary<string, object> Click(UiClickParams p)
        {
            PlayModeNodes.RequirePlayMode(CommandNames.UiClick);
            var node = PlayModeNodes.Require(p.Node).gameObject;
            var path = GameObjectPath.Of(node);
            // Before the handler lookup, which skips inactive objects and would call it missing.
            RequireActive(node);

            // A real click lands on whichever handler is nearest up the hierarchy.
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(node);
            if (handler == null)
            {
                throw new UcpException(UcpErrorCodes.ComponentNotFound,
                    $"Neither '{path}' nor a parent of it takes clicks.",
                    "ui_snapshot lists the Buttons; an inactive node or a disabled component takes no clicks either.");
            }

            RequireInteractable(node, handler.GetComponent<Selectable>());

            var data = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                position = ScreenPoint(node),
                rawPointerPress = node,
                eligibleForClick = true,
            };
            data.pressPosition = data.position;

            // Down, up, click, as a tap sends them: some UI reacts to the press rather than the click.
            data.pointerPress = ExecuteEvents.ExecuteHierarchy(node, data, ExecuteEvents.pointerDownHandler) ?? handler;
            ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);

            return new Dictionary<string, object>
            {
                ["node"] = path,
                ["handler"] = GameObjectPath.Of(handler),
            };
        }

        public static Dictionary<string, object> Input(UiInputParams p)
        {
            PlayModeNodes.RequirePlayMode(CommandNames.UiInput);
            if (p.Text == null)
                throw new ArgumentException("Missing required parameter '--text'. Pass \"\" to clear the field.");

            var node = PlayModeNodes.Require(p.Node);
            var field = InputFieldOn(node);
            if (field == null)
            {
                throw new UcpException(UcpErrorCodes.ComponentNotFound,
                    $"'{GameObjectPath.Of(node)}' has no InputField or TMP_InputField.",
                    "ui_snapshot lists the input fields under inputs.");
            }

            RequireInteractable(node.gameObject, field);

            // Both field types expose the same members, so one reflective path serves them.
            var type = field.GetType();
            type.GetProperty("text")?.SetValue(field, p.Text);
            var text = type.GetProperty("text")?.GetValue(field) as string ?? "";

            var sent = new List<string> { "value_changed" };
            if (p.Submit && Fire(field, "onSubmit", text))
                sent.Add("submit");
            if (Fire(field, "onEndEdit", text))
                sent.Add("end_edit");

            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                // Can differ from the input: the field applies its character limit and content type.
                ["text"] = text,
                ["events"] = sent,
            };
        }

        /// <summary>What <see cref="Completion.UiWaitPolicy"/> checks every frame; kept on the job as JSON.</summary>
        [Serializable]
        public sealed class WaitCondition
        {
            public string Node;
            public string Until;
            public string Text;
        }

        /// <summary>Validates the parameters into the condition ui_wait keeps checking.</summary>
        public static WaitCondition Condition(UiWaitParams p)
        {
            PlayModeNodes.RequirePlayMode(CommandNames.UiWait);
            var until = string.IsNullOrEmpty(p.Until)
                ? p.Text != null ? UiWaitParams.ShowsText : UiWaitParams.Active
                : p.Until;
            if (until == UiWaitParams.ShowsText && string.IsNullOrEmpty(p.Text))
                throw new ArgumentException($"until={UiWaitParams.ShowsText} needs --text.");
            if (until != UiWaitParams.ShowsText && p.Text != null)
                throw new ArgumentException($"--text only applies to until={UiWaitParams.ShowsText}.");

            return new WaitCondition { Node = p.Node.Trim(), Until = until, Text = p.Text };
        }

        /// <param name="seen">What the node looks like now, for the message when the wait gives up.</param>
        /// <returns>The result payload once the condition holds, else null.</returns>
        public static Dictionary<string, object> Check(WaitCondition condition, long elapsedMs, out string seen)
        {
            var node = PlayModeNodes.Resolve(condition.Node);
            if (node == null)
            {
                seen = "it does not exist";
                return null;
            }

            var active = node.gameObject.activeInHierarchy;
            seen = active ? "it exists and is active" : "it exists but is inactive";
            string matched = null;
            switch (condition.Until)
            {
                case UiWaitParams.Exists:
                    break;
                case UiWaitParams.Active:
                    if (!active)
                        return null;
                    break;
                default:
                    var shown = ShownTexts(node).ToList();
                    matched = shown.FirstOrDefault(t => t.IndexOf(condition.Text, StringComparison.Ordinal) >= 0);
                    if (matched != null)
                        break;

                    seen = shown.Count == 0
                        ? $"{seen}, with no active text on or under it"
                        : $"{seen}, showing {string.Join(", ", shown.Take(5).Select(t => $"'{t}'"))}";
                    return null;
            }

            var payload = new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["until"] = condition.Until,
                ["active"] = active,
                ["elapsed_ms"] = elapsedMs,
            };
            if (matched != null)
                payload["text"] = matched;
            return payload;
        }

        public static string Describe(WaitCondition condition) =>
            condition.Until switch
            {
                UiWaitParams.Exists => $"'{condition.Node}' to exist",
                UiWaitParams.Active => $"'{condition.Node}' to be active",
                _ => $"'{condition.Node}' to show '{condition.Text}'",
            };

        static IEnumerable<string> ShownTexts(Transform node)
        {
            foreach (var label in node.GetComponentsInChildren<Text>(false))
                yield return label.text ?? "";

            if (!ComponentTypeResolver.TryResolveComponent(TmpText, out var tmpType))
                yield break;

            var text = tmpType.GetProperty("text");
            foreach (var label in node.GetComponentsInChildren(tmpType, false))
                yield return text?.GetValue(label) as string ?? "";
        }

        static Selectable InputFieldOn(Transform node)
        {
            var field = node.GetComponent<InputField>();
            if (field != null)
                return field;

            return ComponentTypeResolver.TryResolveComponent(TmpInputField, out var tmpType)
                ? node.GetComponent(tmpType) as Selectable
                : null;
        }

        static void RequireActive(GameObject node)
        {
            if (!node.activeInHierarchy)
            {
                throw new UcpException(UcpErrorCodes.NotInteractable, $"'{GameObjectPath.Of(node)}' is inactive.",
                    $"Run {CommandNames.UiWait} with until=active first.");
            }
        }

        static void RequireInteractable(GameObject node, Selectable selectable)
        {
            RequireActive(node);

            // IsInteractable also folds in every CanvasGroup above it.
            if (selectable != null && !selectable.IsInteractable())
            {
                throw new UcpException(UcpErrorCodes.NotInteractable,
                    $"'{GameObjectPath.Of(selectable.transform)}' is not interactable.",
                    "Its Selectable or a CanvasGroup above it has interactable off.");
            }
        }

        /// <returns>False when the field type has no such event (InputField has no onSubmit before Unity 2022).</returns>
        static bool Fire(Component field, string eventName, string text)
        {
            if (field.GetType().GetProperty(eventName)?.GetValue(field) is not UnityEvent<string> unityEvent)
                return false;

            unityEvent.Invoke(text);
            return true;
        }

        /// <summary>Screen position of the node's centre, for handlers that read where the pointer was.</summary>
        static Vector2 ScreenPoint(GameObject node)
        {
            if (node.transform is not RectTransform rect)
                return Vector2.zero;

            var canvas = node.GetComponentInParent<Canvas>();
            var camera = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            return RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
        }
    }
}
