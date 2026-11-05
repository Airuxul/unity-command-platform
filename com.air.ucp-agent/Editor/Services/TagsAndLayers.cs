using System;
using Air.UcpAgent.Commands;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>Validates <c>tag</c> / <c>layer</c> arguments against the project's tag and layer tables.</summary>
    public static class TagsAndLayers
    {
        public static string RequireTag(string tag)
        {
            var trimmed = tag?.Trim();
            if (string.IsNullOrEmpty(trimmed))
                throw new ArgumentException("Parameter 'tag' is required.");

            var tags = InternalEditorUtility.tags;
            if (Array.IndexOf(tags, trimmed) < 0)
            {
                throw new ArgumentException(
                    $"Unknown tag '{trimmed}'. Defined tags: {string.Join(", ", tags)}. Add one with {CommandNames.TagAdd}.");
            }

            return trimmed;
        }

        /// <summary>Resolves a layer given as either a name or an index.</summary>
        public static int RequireLayer(string layer)
        {
            if (string.IsNullOrWhiteSpace(layer))
                throw new ArgumentException("Parameter 'layer' is required.");

            var trimmed = layer.Trim();
            if (int.TryParse(trimmed, out var index))
            {
                if (index < 0 || index > 31)
                    throw new ArgumentException($"Layer index {index} is out of range (0-31).");

                return index;
            }

            var byName = LayerMask.NameToLayer(trimmed);
            if (byName >= 0)
                return byName;

            var defined = Enumerable.Range(0, 32)
                .Select(LayerMask.LayerToName)
                .Where(n => !string.IsNullOrEmpty(n))
                .ToArray();
            throw new ArgumentException(
                $"Unknown layer '{layer}'. Defined layers: {string.Join(", ", defined)}. Add one with {CommandNames.LayerAdd}.");
        }
    }
}
