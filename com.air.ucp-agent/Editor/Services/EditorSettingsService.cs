using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// Project-settings level editor state: play-mode pause, the tag and layer tables, and the
    /// active transform tool.
    /// </summary>
    public static class EditorSettingsService
    {
        const int FirstUserLayer = 8;
        const int LayerCount = 32;

        public static Dictionary<string, object> Pause(PauseParams p)
        {
            if (!EditorApplication.isPlaying)
                throw new InvalidOperationException("Not in play mode. Start it with 'play' first.");

            EditorApplication.isPaused = p?.Paused ?? !EditorApplication.isPaused;

            return new Dictionary<string, object>
            {
                ["is_playing"] = true,
                ["is_paused"] = EditorApplication.isPaused,
            };
        }

        public static Dictionary<string, object> ListTags() =>
            new Dictionary<string, object>
            {
                ["count"] = InternalEditorUtility.tags.Length,
                ["tags"] = InternalEditorUtility.tags.ToList(),
            };

        public static Dictionary<string, object> AddTag(TagParams p)
        {
            var tag = Require(p?.Tag, "tag");
            if (Array.IndexOf(InternalEditorUtility.tags, tag) >= 0)
                throw new InvalidOperationException($"Tag '{tag}' already exists.");

            InternalEditorUtility.AddTag(tag);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                ["tag"] = tag,
                ["added"] = true,
                ["tags"] = InternalEditorUtility.tags.ToList(),
            };
        }

        public static Dictionary<string, object> RemoveTag(TagParams p)
        {
            var tag = Require(p?.Tag, "tag");
            if (Array.IndexOf(InternalEditorUtility.tags, tag) < 0)
                throw new InvalidOperationException($"Tag '{tag}' does not exist.");

            InternalEditorUtility.RemoveTag(tag);
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                ["tag"] = tag,
                ["removed"] = true,
                ["tags"] = InternalEditorUtility.tags.ToList(),
            };
        }

        public static Dictionary<string, object> ListLayers()
        {
            var layers = new List<object>();
            for (var i = 0; i < LayerCount; i++)
            {
                var name = LayerMask.LayerToName(i);
                if (string.IsNullOrEmpty(name))
                    continue;

                layers.Add(new Dictionary<string, object>
                {
                    ["index"] = i,
                    ["name"] = name,
                    ["builtin"] = i < FirstUserLayer,
                });
            }

            return new Dictionary<string, object>
            {
                ["count"] = layers.Count,
                ["first_user_layer"] = FirstUserLayer,
                ["layers"] = layers,
            };
        }

        public static Dictionary<string, object> AddLayer(LayerParams p)
        {
            var layer = Require(p?.Layer, "layer");
            using var tagManager = OpenTagManager();
            var layers = RequireLayersProperty(tagManager);

            var firstEmpty = -1;
            for (var i = 0; i < LayerCount; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (slot != null && string.Equals(slot.stringValue, layer, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Layer '{layer}' already exists at index {i}.");

                if (firstEmpty == -1 && i >= FirstUserLayer && (slot == null || string.IsNullOrEmpty(slot.stringValue)))
                    firstEmpty = i;
            }

            if (firstEmpty == -1)
                throw new InvalidOperationException("No empty user layer slots left (8-31 are all taken).");

            layers.GetArrayElementAtIndex(firstEmpty).stringValue = layer;
            tagManager.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                ["layer"] = layer,
                ["index"] = firstEmpty,
                ["added"] = true,
            };
        }

        public static Dictionary<string, object> RemoveLayer(LayerParams p)
        {
            var layer = Require(p?.Layer, "layer");
            using var tagManager = OpenTagManager();
            var layers = RequireLayersProperty(tagManager);

            for (var i = FirstUserLayer; i < LayerCount; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (slot == null || !string.Equals(slot.stringValue, layer, StringComparison.OrdinalIgnoreCase))
                    continue;

                slot.stringValue = string.Empty;
                tagManager.ApplyModifiedProperties();
                AssetDatabase.SaveAssets();

                return new Dictionary<string, object>
                {
                    ["layer"] = layer,
                    ["index"] = i,
                    ["removed"] = true,
                };
            }

            throw new InvalidOperationException(
                $"User layer '{layer}' not found. Built-in layers (0-7) cannot be removed.");
        }

        public static Dictionary<string, object> SetTool(ToolSetParams p)
        {
            var name = Require(p?.Tool, "tool");
            if (!Enum.TryParse(name, ignoreCase: true, out Tool tool) || tool > Tool.Custom)
            {
                throw new ArgumentException(
                    $"Unknown tool '{name}'. Valid: {string.Join(", ", Enum.GetNames(typeof(Tool)).Select(n => n.ToLowerInvariant()))}.");
            }

            Tools.current = tool;

            return new Dictionary<string, object>
            {
                ["tool"] = Tools.current.ToString(),
            };
        }

        static SerializedObject OpenTagManager()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
                throw new InvalidOperationException("Could not open ProjectSettings/TagManager.asset.");

            return new SerializedObject(assets[0]);
        }

        static SerializedProperty RequireLayersProperty(SerializedObject tagManager)
        {
            var layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray)
                throw new InvalidOperationException("TagManager.asset has no 'layers' array.");

            return layers;
        }

        static string Require(string value, string paramName)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new ArgumentException($"Parameter '{paramName}' is required.");

            return trimmed;
        }
    }
}
