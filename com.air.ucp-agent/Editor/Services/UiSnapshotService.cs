using System;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Air.UcpAgent.Editor.Services
{
    public static class UiSnapshotService
    {
        public static Dictionary<string, object> Capture()
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException($"{CommandNames.UiSnapshot} runs in play mode. Run {CommandNames.Play} first.");

            var canvases = UnityEngine.Object.FindObjectsOfType<Canvas>(true);
            var list = new List<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                var canvas = canvases[i];
                if (canvas == null || EditorUtility.IsPersistent(canvas))
                    continue;
                if (!canvas.gameObject.scene.IsValid())
                    continue;
                list.Add(canvas);
            }

            list.Sort(CompareCanvas);
            // TextMeshPro is an optional package, so it is looked up by name rather than referenced.
            ComponentTypeResolver.TryResolveComponent("TMPro.TextMeshProUGUI", out var tmpType);
            var rows = new List<Dictionary<string, object>>(list.Count);
            for (int i = 0; i < list.Count; i++)
                rows.Add(DescribeCanvas(list[i], tmpType));

            return new Dictionary<string, object>
            {
                ["canvases"] = rows,
            };
        }

        static int CompareCanvas(Canvas a, Canvas b)
        {
            var order = a.sortingOrder.CompareTo(b.sortingOrder);
            if (order != 0)
                return order;
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        }

        static Dictionary<string, object> DescribeCanvas(Canvas canvas, Type tmpType)
        {
            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(canvas.transform),
                ["name"] = canvas.name,
                ["active"] = canvas.gameObject.activeInHierarchy,
                ["render_mode"] = canvas.renderMode.ToString(),
                ["sorting_order"] = canvas.sortingOrder,
                ["texts"] = Texts(canvas, tmpType),
                ["buttons"] = Buttons(canvas, tmpType),
            };
        }

        static List<Dictionary<string, object>> Texts(Canvas canvas, Type tmpType)
        {
            var rows = new List<Dictionary<string, object>>();
            var labels = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
                rows.Add(TextRow(labels[i].transform, labels[i].text, labels[i].gameObject.activeInHierarchy));

            if (tmpType == null)
                return rows;

            var tmpLabels = canvas.GetComponentsInChildren(tmpType, true);
            for (int i = 0; i < tmpLabels.Length; i++)
            {
                if (tmpLabels[i] is not Component component)
                    continue;
                var text = tmpType.GetProperty("text")?.GetValue(component) as string ?? "";
                rows.Add(TextRow(component.transform, text, component.gameObject.activeInHierarchy));
            }

            return rows;
        }

        static List<Dictionary<string, object>> Buttons(Canvas canvas, Type tmpType)
        {
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            var rows = new List<Dictionary<string, object>>(buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                rows.Add(new Dictionary<string, object>
                {
                    ["node"] = GameObjectPath.Of(button.transform),
                    ["name"] = button.name,
                    ["text"] = ButtonText(button, tmpType),
                    ["interactable"] = button.IsInteractable(),
                    ["active"] = button.gameObject.activeInHierarchy,
                });
            }

            return rows;
        }

        static string ButtonText(Button button, Type tmpType)
        {
            var label = button.GetComponentInChildren<Text>(true);
            if (label != null && !string.IsNullOrEmpty(label.text))
                return label.text;
            if (tmpType == null)
                return label != null ? label.text : "";

            var tmp = button.GetComponentInChildren(tmpType, true);
            if (tmp == null)
                return label != null ? label.text : "";
            return tmpType.GetProperty("text")?.GetValue(tmp) as string ?? "";
        }

        static Dictionary<string, object> TextRow(Transform node, string text, bool active)
        {
            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(node),
                ["name"] = node.name,
                ["text"] = text ?? "",
                ["active"] = active,
            };
        }
    }
}
