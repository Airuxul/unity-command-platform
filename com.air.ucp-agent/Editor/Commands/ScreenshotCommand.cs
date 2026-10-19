using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Air.UcpAgent.Invoke;
using UnityEditor;
using UnityEngine;
using Air.UcpAgent.Params;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;

namespace Air.UcpAgent.Editor.Commands
{
    public class ScreenshotCommand : CliCommand<ScreenshotParams>
    {
        public override InvokeDescriptor Descriptor { get; } = new InvokeDescriptor<ScreenshotParams>(
            CommandNames.Screenshot,
            CommandHostScope.Editor,
            "Capture Scene view or the Game view frame to PNG. Game view includes Screen Space Overlay canvases.");

        private const int DefaultWidth = 1920;
        private const int DefaultHeight = 1080;

        public override void Run(ScreenshotParams p)
        {
            (bool ok, object data, string error) result;
            try
            {
                var view = (p.View ?? "scene").ToLowerInvariant();
                var width = p.Width ?? DefaultWidth;
                var height = p.Height ?? DefaultHeight;
                var outputPath = ResolveOutputPath(p.OutputPath);

                var dir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                switch (view)
                {
                    case "scene":
                        result = CaptureScene(width, height, outputPath);
                        break;
                    case "game":
                        result = CaptureGame(width, height, outputPath);
                        break;
                    default:
                        result = Fail($"Unknown view '{view}'. Valid: scene, game.");
                        break;
                }
            }
            catch (Exception ex)
            {
                result = Fail($"Screenshot failed: {ex.Message}");
            }

            if (result.ok)
                CompleteSuccess(InvokeResult.Ok("screenshot captured", result.data));
            else
                CompleteFail(result.error);
        }

        private static (bool ok, object data, string error) CaptureScene(int width, int height, string outputPath)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return Fail("No active SceneView found.");

            var camera = sceneView.camera;
            if (camera == null)
                return Fail("SceneView camera is null.");

            return CaptureCamera(camera, width, height, outputPath);
        }

        private static (bool ok, object data, string error) CaptureGame(int width, int height, string outputPath)
        {
            var frame = GameViewFrame();
            if (frame == null)
                return Fail("Game view has no frame. Open the Game view and try again.");

            RenderTexture scaled = null;
            Texture2D tex = null;
            var previous = RenderTexture.active;
            try
            {
                scaled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(frame, scaled);
                RenderTexture.active = scaled;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(outputPath, tex.EncodeToPNG());
                return Success(new Dictionary<string, object>
                {
                    ["path"] = outputPath,
                    ["width"] = width,
                    ["height"] = height,
                });
            }
            finally
            {
                RenderTexture.active = previous;
                if (scaled != null)
                    RenderTexture.ReleaseTemporary(scaled);
                if (tex != null)
                    UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        static RenderTexture GameViewFrame()
        {
            var frame = GameViewTexture();
            if (frame != null)
                return frame;

            var gameView = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameView == null)
                return null;
            var window = EditorWindow.GetWindow(gameView, false, null, false);
            window.Show();
            window.Repaint();
            return GameViewTexture();
        }

        static RenderTexture GameViewTexture()
        {
            var editor = typeof(UnityEditor.Editor).Assembly;
            var frame = InvokeTextureGetter(editor.GetType("UnityEditor.PlayModeView"), "GetMainPlayModeViewRenderTexture");
            if (frame != null)
                return frame;
            return InvokeTextureGetter(editor.GetType("UnityEditor.GameView"), "GetMainGameViewRenderTexture");
        }

        static RenderTexture InvokeTextureGetter(Type type, string name)
        {
            var getter = FindStaticGetter(type, name);
            if (getter == null)
                return null;
            var args = getter.GetParameters().Length == 0 ? null : new object[] { 0 };
            return getter.Invoke(null, args) as RenderTexture;
        }

        static MethodInfo FindStaticGetter(Type type, string name)
        {
            if (type == null)
                return null;
            MethodInfo zero = null;
            MethodInfo one = null;
            var methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name != name)
                    continue;
                var count = methods[i].GetParameters().Length;
                if (count == 0)
                    zero = methods[i];
                else if (count == 1)
                    one = methods[i];
            }

            return zero ?? one;
        }

        private static (bool ok, object data, string error) CaptureCamera(Camera camera, int width, int height, string outputPath)
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

                return Success(new Dictionary<string, object>
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

        private static (bool ok, object data, string error) Success(object data) => (true, data, null);
        private static (bool ok, object data, string error) Fail(string error) => (false, null, error);

    }
}
