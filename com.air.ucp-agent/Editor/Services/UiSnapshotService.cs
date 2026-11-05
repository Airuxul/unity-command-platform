using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Air.UcpAgent.Editor.Services
{
    public static class UiSnapshotService
    {
        const int DefaultLimit = 500;

        public static Dictionary<string, object> Capture(UiSnapshotParams p)
        {
            if (!EditorApplication.isPlaying)
                throw new UcpException(UcpErrorCodes.PlayModeRequired, $"{CommandNames.UiSnapshot} runs in play mode.", $"Run {CommandNames.Play} first.");

            var root = string.IsNullOrWhiteSpace(p?.Root) ? null : p.Root.Trim().Replace('\\', '/').TrimEnd('/');
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
            if (root != null)
                list = KeepAround(list, root);

            // TextMeshPro is an optional package, so it is looked up by name rather than referenced.
            var tmpType = ComponentTypeResolver.FindOptional("TMPro.TextMeshProUGUI");
            var tmpInputType = ComponentTypeResolver.FindOptional("TMPro.TMP_InputField");
            var budget = new Budget(p?.Limit is > 0 ? p.Limit.Value : DefaultLimit, root);
            var rows = new List<Dictionary<string, object>>(list.Count);
            for (int i = 0; i < list.Count; i++)
                rows.Add(DescribeCanvas(list[i], tmpType, tmpInputType, budget));

            return new Dictionary<string, object>
            {
                ["canvases"] = rows,
                ["row_count"] = budget.Emitted,
                ["row_count_total"] = budget.Total,
                ["truncated"] = budget.Total > budget.Emitted,
                ["limit"] = budget.Limit,
            };
        }

        /// <summary>A root above a canvas keeps the whole canvas; one inside it keeps that part.</summary>
        static List<Canvas> KeepAround(List<Canvas> canvases, string root)
        {
            var paths = canvases.Select(c => GameObjectPath.Of(c.transform)).ToList();
            var kept = Enumerable.Range(0, canvases.Count)
                .Where(i => Under(paths[i], root) || root.StartsWith(paths[i] + "/", StringComparison.Ordinal))
                .Select(i => canvases[i])
                .ToList();
            if (kept.Count > 0)
                return kept;

            throw new UcpException(
                UcpErrorCodes.NodeNotFound,
                $"No canvas at or around '{root}'.",
                paths.Count == 0 ? "No canvas is loaded." : $"Canvases: {string.Join(", ", paths.Distinct().Take(30))}.");
        }

        static bool Under(string path, string root) =>
            path == root || path.StartsWith(root + "/", StringComparison.Ordinal);

        /// <summary>Rows left to report, shared by every list of every canvas, in that order.</summary>
        sealed class Budget
        {
            readonly string _root;

            public Budget(int limit, string root)
            {
                Limit = limit;
                _root = root;
            }

            public int Limit { get; }
            public int Emitted { get; private set; }
            public int Total { get; private set; }

            /// <summary>Counts the row if it is under the root; true if it still fits.</summary>
            public bool Take(string path)
            {
                if (_root != null && !Under(path, _root))
                    return false;

                Total++;
                if (Emitted >= Limit)
                    return false;

                Emitted++;
                return true;
            }
        }

        static int CompareCanvas(Canvas a, Canvas b)
        {
            var order = a.sortingOrder.CompareTo(b.sortingOrder);
            if (order != 0)
                return order;
            return string.Compare(a.name, b.name, StringComparison.Ordinal);
        }

        static Dictionary<string, object> DescribeCanvas(Canvas canvas, Type tmpType, Type tmpInputType, Budget budget)
        {
            return new Dictionary<string, object>
            {
                ["node"] = GameObjectPath.Of(canvas.transform),
                ["name"] = canvas.name,
                ["active"] = canvas.gameObject.activeInHierarchy,
                ["render_mode"] = canvas.renderMode.ToString(),
                ["sorting_order"] = canvas.sortingOrder,
                ["texts"] = Texts(canvas, tmpType, budget),
                ["buttons"] = Buttons(canvas, tmpType, budget),
                ["inputs"] = Inputs(canvas, tmpInputType, budget),
            };
        }

        static List<Dictionary<string, object>> Texts(Canvas canvas, Type tmpType, Budget budget)
        {
            var rows = new List<Dictionary<string, object>>();
            var labels = canvas.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < labels.Length; i++)
                AddTextRow(rows, budget, labels[i].transform, labels[i].text, labels[i].gameObject.activeInHierarchy);

            if (tmpType == null)
                return rows;

            var tmpLabels = canvas.GetComponentsInChildren(tmpType, true);
            for (int i = 0; i < tmpLabels.Length; i++)
            {
                if (tmpLabels[i] is not Component component)
                    continue;
                AddTextRow(rows, budget, component.transform, ReadText(tmpType, component), component.gameObject.activeInHierarchy);
            }

            return rows;
        }

        static List<Dictionary<string, object>> Buttons(Canvas canvas, Type tmpType, Budget budget)
        {
            var buttons = canvas.GetComponentsInChildren<Button>(true);
            var rows = new List<Dictionary<string, object>>(buttons.Length);
            for (int i = 0; i < buttons.Length; i++)
            {
                var button = buttons[i];
                var path = GameObjectPath.Of(button.transform);
                if (!budget.Take(path))
                    continue;

                rows.Add(new Dictionary<string, object>
                {
                    ["node"] = path,
                    ["name"] = button.name,
                    ["text"] = ButtonText(button, tmpType),
                    ["interactable"] = button.IsInteractable(),
                    ["active"] = button.gameObject.activeInHierarchy,
                });
            }

            return rows;
        }

        /// <summary>InputField, and TMP_InputField when TextMeshPro is installed; both are Selectables.</summary>
        static List<Dictionary<string, object>> Inputs(Canvas canvas, Type tmpInputType, Budget budget)
        {
            var rows = new List<Dictionary<string, object>>();
            foreach (var field in canvas.GetComponentsInChildren<InputField>(true))
            {
                var placeholder = field.placeholder as Text;
                AddInputRow(rows, budget, field, field.text, placeholder != null ? placeholder.text : "");
            }

            if (tmpInputType == null)
                return rows;

            foreach (var component in canvas.GetComponentsInChildren(tmpInputType, true))
            {
                if (component is not Selectable field)
                    continue;

                var placeholder = tmpInputType.GetProperty("placeholder")?.GetValue(component) as Component;
                AddInputRow(rows, budget, field, ReadText(tmpInputType, component),
                    placeholder != null ? ReadText(placeholder.GetType(), placeholder) : "");
            }

            return rows;
        }

        static void AddInputRow(List<Dictionary<string, object>> rows, Budget budget, Selectable field, string text, string placeholder)
        {
            var path = GameObjectPath.Of(field.transform);
            if (!budget.Take(path))
                return;

            rows.Add(new Dictionary<string, object>
            {
                ["node"] = path,
                ["name"] = field.name,
                ["text"] = text ?? "",
                ["placeholder"] = placeholder ?? "",
                ["interactable"] = field.IsInteractable(),
                ["active"] = field.gameObject.activeInHierarchy,
            });
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
            return ReadText(tmpType, tmp);
        }

        static string ReadText(Type type, Component component) =>
            type.GetProperty("text")?.GetValue(component) as string ?? "";

        static void AddTextRow(List<Dictionary<string, object>> rows, Budget budget, Transform node, string text, bool active)
        {
            var path = GameObjectPath.Of(node);
            if (!budget.Take(path))
                return;

            rows.Add(new Dictionary<string, object>
            {
                ["node"] = path,
                ["name"] = node.name,
                ["text"] = text ?? "",
                ["active"] = active,
            });
        }
    }
}
