using System;
using System.Collections.Generic;
using System.IO;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using UnityEditor;

namespace Air.UcpAgent.Editor.Services.Assets
{
    /// <summary>
    /// The folder search behind asset_list / prefab_list / scene_list: one folder rule, one name
    /// filter, and one way of counting what the limit cut off.
    /// </summary>
    public static class AssetQuery
    {
        public const int DefaultLimit = 200;

        public sealed class Page
        {
            public string Folder;
            public readonly List<object> Rows = new List<object>();

            /// <summary>Sub-folders met on the way; they are never rows and never name-filtered.</summary>
            public readonly List<object> Folders = new List<object>();

            /// <summary>Every match, including those past the limit.</summary>
            public int Total;

            /// <summary>The fields every list payload shares, with the rows under <paramref name="rowsKey"/>.</summary>
            public Dictionary<string, object> Describe(string rowsKey) =>
                new Dictionary<string, object>
                {
                    ["folder"] = Folder,
                    ["count"] = Rows.Count,
                    ["total_matches"] = Total,
                    ["truncated"] = Total > Rows.Count,
                    [rowsKey] = Rows,
                };
        }

        /// <param name="filter">AssetDatabase filter such as <c>t:Prefab</c>; empty matches everything.</param>
        /// <param name="row">Builds the row for a match within the limit.</param>
        /// <param name="include">Extra filter, applied before counting; null keeps every match.</param>
        public static Page InFolder(
            string folder,
            string filter,
            string name,
            int? limit,
            Func<string, string, Dictionary<string, object>> row,
            Func<string, bool> include = null,
            bool recursive = true)
        {
            var page = new Page { Folder = RequireFolder(folder) };
            var max = limit ?? DefaultLimit;

            foreach (var guid in AssetDatabase.FindAssets(filter ?? string.Empty, new[] { page.Folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!recursive &&
                    !string.Equals(AssetPaths.ParentFolder(path), page.Folder, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (AssetDatabase.IsValidFolder(path))
                {
                    page.Folders.Add(path);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(name) &&
                    Path.GetFileNameWithoutExtension(path).IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (include != null && !include(path))
                    continue;

                page.Total++;
                if (page.Rows.Count < max)
                    page.Rows.Add(row(path, guid));
            }

            return page;
        }

        static string RequireFolder(string folder)
        {
            var resolved = string.IsNullOrWhiteSpace(folder)
                ? "Assets"
                : AssetPaths.RequireProjectPath(folder, ParamKeys.Folder);

            if (!AssetDatabase.IsValidFolder(resolved))
                throw new ArgumentException($"'{resolved}' is not a folder.");

            return resolved;
        }
    }
}
