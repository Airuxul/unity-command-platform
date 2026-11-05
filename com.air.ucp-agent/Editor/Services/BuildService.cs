using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>Player builds through BuildPipeline.BuildPlayer, which blocks the Editor until it is done.</summary>
    public static class BuildService
    {
        const int MaxMessages = 20;

        /// <returns>The report summary; <paramref name="succeeded"/> says whether BuildPipeline called it a success.</returns>
        public static Dictionary<string, object> Build(BuildParams p, out bool succeeded)
        {
            if (EditorApplication.isPlaying)
                throw new UcpException(UcpErrorCodes.EditModeRequired, $"{CommandNames.Build} runs in Edit Mode.", $"Run {CommandNames.Stop} first.");
            if (EditorApplication.isCompiling)
                throw new InvalidOperationException("Scripts are compiling; build once they are done.");

            var target = Target(p.Target);
            var scenes = Scenes(p.Scenes);
            var output = OutputPath(p.OutputPath);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = p.Development ? BuildOptions.Development : BuildOptions.None,
            });

            var summary = report.summary;
            succeeded = summary.result == BuildResult.Succeeded;
            return new Dictionary<string, object>
            {
                ["result"] = summary.result.ToString(),
                ["target"] = summary.platform.ToString(),
                ["output_path"] = summary.outputPath.Replace('\\', '/'),
                ["scenes"] = scenes,
                // A scene builds from its file, so changes not saved yet are not in the player.
                ["unsaved_scenes"] = UnsavedAmong(scenes),
                ["development"] = p.Development,
                ["size_bytes"] = (long)summary.totalSize,
                ["duration_s"] = Math.Round(summary.totalTime.TotalSeconds, 1),
                ["error_count"] = summary.totalErrors,
                ["warning_count"] = summary.totalWarnings,
                ["messages"] = report.steps
                    .SelectMany(step => step.messages)
                    .Where(m => m.type is LogType.Error or LogType.Exception)
                    .Select(m => m.content)
                    .Distinct()
                    .Take(MaxMessages)
                    .ToList(),
            };
        }

        static BuildTarget Target(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return EditorUserBuildSettings.activeBuildTarget;

            // Enum.TryParse also takes numbers and obsolete members; only current names are valid.
            var valid = typeof(BuildTarget).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => !f.IsDefined(typeof(ObsoleteAttribute), false))
                .Select(f => (BuildTarget)f.GetValue(null))
                .Where(t => (int)t > 0)
                .ToList();
            var target = valid.FirstOrDefault(t => string.Equals(t.ToString(), name.Trim(), StringComparison.OrdinalIgnoreCase));
            var installed = valid
                .Where(t => BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(t), t))
                .Select(t => t.ToString())
                .Distinct()
                .ToList();
            if (target == 0)
            {
                throw new ArgumentException($"Unknown build target '{name}'. Installed: {string.Join(", ", installed)}.");
            }

            if (!installed.Contains(target.ToString()))
            {
                throw new ArgumentException(
                    $"The {target} build support module is not installed. Installed: {string.Join(", ", installed)}.");
            }

            return target;
        }

        static string[] Scenes(string[] requested)
        {
            var scenes = requested?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(AssetPaths.Normalize).ToArray();
            if (scenes == null || scenes.Length == 0)
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                if (scenes.Length == 0)
                    throw new ArgumentException("Build Settings has no enabled scenes. Pass scenes, e.g. [\"Assets/Scenes/Main.unity\"].");
                return scenes;
            }

            var missing = scenes.Where(s => AssetDatabase.LoadAssetAtPath<SceneAsset>(s) == null).ToList();
            if (missing.Count > 0)
                throw new ArgumentException($"Not scene assets: {string.Join(", ", missing)}.");
            return scenes;
        }

        static string OutputPath(string requested)
        {
            var root = EditorProjectPaths.GetProjectPath();
            var full = Path.GetFullPath(Path.Combine(root, requested.Trim()));
            var assets = Path.GetFullPath(Path.Combine(root, "Assets")) + Path.DirectorySeparatorChar;
            // Unity would import the player it just built as assets.
            if (full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"'{ParamKeys.OutputPath}' must not be under Assets/.");
            return full;
        }

        static List<string> UnsavedAmong(string[] scenes) =>
            Enumerable.Range(0, SceneManager.sceneCount)
                .Select(SceneManager.GetSceneAt)
                .Where(s => s.isDirty && scenes.Contains(s.path))
                .Select(s => s.path)
                .ToList();
    }
}
