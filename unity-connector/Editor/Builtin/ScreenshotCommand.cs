using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityCliConnector.Builtin
{
    [CliCommand(
        "editor.screenshot",
        Scope = CommandScope.Editor,
        Description = "Capture Scene or Game view to PNG",
        Aliases = "screenshot")]
    public static class ScreenshotCommand
    {
        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        public static CommandResult Run(CliParams p)
        {
            try
            {
                var view = (p.GetString("view", "scene") ?? "scene").ToLowerInvariant();
                var width = p.GetInt("width") ?? DefaultWidth;
                var height = p.GetInt("height") ?? DefaultHeight;
                var outputPath = ResolveOutputPath(p.GetString("output_path"));

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                switch (view)
                {
                    case "scene":
                        return CaptureScene(width, height, outputPath);
                    case "game":
                        return CaptureGame(width, height, outputPath);
                    default:
                        return CommandResult.Fail($"Unknown view '{view}'. Valid: scene, game.");
                }
            }
            catch (Exception ex)
            {
                return CommandResult.Fail($"Screenshot failed: {ex.Message}");
            }
        }

        private static CommandResult CaptureScene(int width, int height, string outputPath)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return CommandResult.Fail("No active SceneView found.");

            var camera = sceneView.camera;
            if (camera == null)
                return CommandResult.Fail("SceneView camera is null.");

            return CaptureCamera(camera, width, height, outputPath);
        }

        private static CommandResult CaptureGame(int width, int height, string outputPath)
        {
            var camera = Camera.main;
            if (camera == null)
                camera = UnityEngine.Object.FindObjectOfType<Camera>();

            if (camera == null)
                return CommandResult.Fail("No camera found in scene.");

            return CaptureCamera(camera, width, height, outputPath);
        }

        private static CommandResult CaptureCamera(Camera camera, int width, int height, string outputPath)
        {
            var previousRt = camera.targetTexture;
            RenderTexture rt = null;
            Texture2D tex = null;

            try
            {
                rt = new RenderTexture(width, height, 24);
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());

                return CommandResult.Success(new Dictionary<string, object>
                {
                    ["path"] = outputPath,
                    ["width"] = width,
                    ["height"] = height,
                });
            }
            finally
            {
                camera.targetTexture = previousRt;
                RenderTexture.active = null;
                if (rt != null)
                    UnityEngine.Object.DestroyImmediate(rt);
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string ResolveOutputPath(string userPath)
        {
            if (string.IsNullOrEmpty(userPath))
                userPath = "Screenshots/screenshot.png";

            if (Path.IsPathRooted(userPath))
                return Path.GetFullPath(userPath);

            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.GetFullPath(Path.Combine(projectRoot, userPath));
        }
    }
}
