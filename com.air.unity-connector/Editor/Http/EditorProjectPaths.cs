using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Air.UnityConnector
{
    /// <summary>Stable per-project paths under ~/.unity-cmd/.</summary>
    internal static class EditorProjectPaths
    {
        public static string GetProjectPath()
        {
            var assets = Application.dataPath;
            if (assets.EndsWith("/Assets", StringComparison.Ordinal))
                return assets[..^6];
            if (assets.EndsWith("\\Assets", StringComparison.Ordinal))
                return assets[..^7];
            return assets;
        }

        public static string GetProjectHash()
        {
            var normalized = GetProjectPath().Replace('\\', '/').ToLowerInvariant();
            using var md5 = MD5.Create();
            var hash = BitConverter.ToString(md5.ComputeHash(Encoding.UTF8.GetBytes(normalized)))
                .Replace("-", "", StringComparison.Ordinal)
                .ToLowerInvariant();
            return hash.Length > 16 ? hash[..16] : hash;
        }

        public static string InstancesFilePath() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-cmd",
                "instances",
                $"{GetProjectHash()}.json");

        public static string JobsDirectory() =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".unity-cmd",
                "jobs",
                GetProjectHash());
    }
}
