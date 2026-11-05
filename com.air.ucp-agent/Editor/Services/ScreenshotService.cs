using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Editor.IO;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// PNG capture of the Scene view, the Game view, or a UI canvas.
    /// </summary>
    /// <remarks>
    /// The scene/game paths are a plain <c>camera.Render()</c> into a RenderTexture, which by
    /// construction cannot include a <c>ScreenSpaceOverlay</c> canvas — Unity composites overlay UI
    /// outside camera rendering. That is why <c>view=ui</c> exists and why it has to put the canvas
    /// on a camera to capture it at all.
    /// </remarks>
    public static class ScreenshotService
    {
        const int DefaultWidth = 1920;
        const int DefaultHeight = 1080;
        const string TempCameraName = "__ucp_ui_capture_camera";

        public static Dictionary<string, object> Capture(ScreenshotParams p)
        {
            var view = (p.View ?? "scene").Trim().ToLowerInvariant();
            var outputPath = ResolveOutputPath(p.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

            return view switch
            {
                "scene" => CaptureSceneView(p, outputPath),
                "game" => CaptureGameView(p, outputPath),
                "ui" => CaptureUi(p, outputPath),
                _ => throw new ArgumentException($"Unknown view '{view}'. Valid: scene, game, ui."),
            };
        }

        static Dictionary<string, object> CaptureSceneView(ScreenshotParams p, string outputPath)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                throw new InvalidOperationException("No active SceneView found.");
            if (sceneView.camera == null)
                throw new InvalidOperationException("The SceneView camera is null.");

            var result = CaptureCamera(sceneView.camera, p.Width ?? DefaultWidth, p.Height ?? DefaultHeight,
                outputPath, alpha: false);
            result["view"] = "scene";
            // Say so rather than let a caller wonder why the UI is missing from the picture.
            result["note"] = "Camera render: gizmos and ScreenSpaceOverlay UI are not included. Use view=ui for UI.";
            return result;
        }

        static Dictionary<string, object> CaptureGameView(ScreenshotParams p, string outputPath)
        {
            var camera = Camera.main ?? Object.FindObjectOfType<Camera>();
            if (camera == null)
            {
                throw new InvalidOperationException(
                    "No camera found in the scene. Tag one as MainCamera, or add any Camera.");
            }

            var result = CaptureCamera(camera, p.Width ?? DefaultWidth, p.Height ?? DefaultHeight,
                outputPath, alpha: false);
            result["view"] = "game";
            result["camera"] = camera.name;
            result["note"] = "Camera render: ScreenSpaceOverlay UI is not included. Use view=ui for UI.";
            return result;
        }

        // --- UI capture --------------------------------------------------------------------

        static Dictionary<string, object> CaptureUi(ScreenshotParams p, string outputPath)
        {
            var hasPrefab = !string.IsNullOrWhiteSpace(p.Source);
            var hasNode = !string.IsNullOrWhiteSpace(p.Node);
            if (hasPrefab == hasNode)
            {
                throw new ArgumentException(
                    $"view=ui needs exactly one of '{ParamKeys.Source}' (a prefab: renders a throwaway copy, no side effects) " +
                    $"or '{ParamKeys.Node}' (renders the live canvas in the open scene).");
            }

            return hasPrefab ? CaptureUiFromPrefab(p, outputPath) : CaptureUiFromScene(p, outputPath);
        }

        /// <summary>
        /// Renders a copy of the prefab in a throwaway preview scene. The asset and every open scene
        /// are untouched, which makes this the safe way to look at a prefab.
        /// </summary>
        static Dictionary<string, object> CaptureUiFromPrefab(ScreenshotParams p, string outputPath)
        {
            var prefabPath = AssetPaths.RequireExisting(p.Source, ParamKeys.Source);
            var asset = AssetPaths.RequireAsset<GameObject>(prefabPath, ParamKeys.Source);

            using var stage = PreviewStage.Begin();
            var copy = PrefabUtility.InstantiatePrefab(asset, stage.Scene) as GameObject;
            if (copy == null)
                throw new InvalidOperationException($"Could not instantiate '{prefabPath}'.");

            stage.Track(copy);

            var canvas = FindCanvas(copy, prefabPath);
            var result = RenderCanvas(canvas, p, outputPath, stage.Scene);
            result["source"] = "prefab_copy";
            result["prefab"] = prefabPath;
            return result;
        }

        static Dictionary<string, object> CaptureUiFromScene(ScreenshotParams p, string outputPath)
        {
            // 'node' is documented as a node in an open scene, so the Prefab Mode stage is not searched.
            using var scope = EditTargetScope.PeekScene(p.Scene);
            var node = GameObjectPath.Require(scope, p.Node, ParamKeys.Node);
            var canvas = FindCanvas(node.gameObject, p.Node);

            var result = RenderCanvas(canvas, p, outputPath, canvas.gameObject.scene);
            result["source"] = "scene_node";
            result["node"] = GameObjectPath.Of(node);
            return result;
        }

        static Canvas FindCanvas(GameObject root, string label)
        {
            var canvas = root.GetComponent<Canvas>()
                         ?? root.GetComponentInParent<Canvas>()
                         ?? root.GetComponentInChildren<Canvas>(includeInactive: true);
            if (canvas == null)
                throw new InvalidOperationException($"No Canvas on or around '{label}' — nothing to render.");

            return canvas;
        }

        /// <summary>
        /// Puts the canvas on a temporary camera so it becomes camera-renderable, captures, then
        /// restores every field it touched. An overlay canvas cannot be rendered any other way.
        /// </summary>
        static Dictionary<string, object> RenderCanvas(
            Canvas canvas,
            ScreenshotParams p,
            string outputPath,
            UnityEngine.SceneManagement.Scene scene)
        {
            var (width, height) = ResolveUiSize(canvas, p);

            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousDistance = canvas.planeDistance;
            GameObject cameraHolder = null;

            try
            {
                cameraHolder = new GameObject(TempCameraName) { hideFlags = HideFlags.HideAndDontSave };
                if (scene.IsValid())
                    UnityEditor.SceneManagement.EditorSceneManager.MoveGameObjectToScene(cameraHolder, scene);

                var camera = cameraHolder.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = ResolveBackground(p.Background);
                camera.orthographic = false;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 5000f;
                // Only the layers the canvas subtree actually uses, so scene geometry cannot leak in.
                camera.cullingMask = CullingMaskOf(canvas);
                camera.enabled = false;
                // A camera in a preview scene renders nothing unless it is explicitly bound to it.
                if (scene.IsValid())
                    camera.scene = scene;

                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 100f;

                // Without this the capture shows the layout from before the render-mode switch.
                Canvas.ForceUpdateCanvases();

                var result = CaptureCamera(camera, width, height, outputPath, alpha: true);
                result["view"] = "ui";
                result["canvas"] = canvas.name;
                result["culling_mask"] = camera.cullingMask;
                return result;
            }
            finally
            {
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                Canvas.ForceUpdateCanvases();

                if (cameraHolder != null)
                    Object.DestroyImmediate(cameraHolder);
            }
        }

        /// <summary>Defaults to the scaler's reference resolution so the image matches the design ratio.</summary>
        static (int width, int height) ResolveUiSize(Canvas canvas, ScreenshotParams p)
        {
            if (p.Width.HasValue && p.Height.HasValue)
                return (p.Width.Value, p.Height.Value);

            var reference = canvas.GetComponent<CanvasScaler>()?.referenceResolution
                            ?? new Vector2(DefaultWidth, DefaultHeight);
            var width = p.Width ?? Mathf.RoundToInt(reference.x);
            var height = p.Height ?? Mathf.RoundToInt(reference.y);

            return (Mathf.Max(16, width), Mathf.Max(16, height));
        }

        static int CullingMaskOf(Canvas canvas)
        {
            var mask = 0;
            foreach (var node in GameObjectPath.Descend(canvas.transform))
                mask |= 1 << node.gameObject.layer;

            return mask;
        }

        static Color ResolveBackground(string background)
        {
            if (string.IsNullOrWhiteSpace(background))
                return new Color(0f, 0f, 0f, 0f);

            var text = background.Trim();
            if (string.Equals(text, "transparent", StringComparison.OrdinalIgnoreCase))
                return new Color(0f, 0f, 0f, 0f);

            var html = text.StartsWith("#", StringComparison.Ordinal) ? text : "#" + text;
            if (ColorUtility.TryParseHtmlString(html, out var parsed))
                return parsed;

            throw new ArgumentException($"Could not parse background '{background}'. Use #RRGGBB, #RRGGBBAA or 'transparent'.");
        }

        // --- shared ------------------------------------------------------------------------

        static Dictionary<string, object> CaptureCamera(
            Camera camera,
            int width,
            int height,
            string outputPath,
            bool alpha)
        {
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            RenderTexture rt = null;
            Texture2D texture = null;

            try
            {
                rt = new RenderTexture(width, height, 24)
                {
                    antiAliasing = 1,
                };
                camera.targetTexture = rt;
                camera.Render();

                RenderTexture.active = rt;
                texture = new Texture2D(width, height, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());

                return new Dictionary<string, object>
                {
                    ["path"] = outputPath,
                    ["width"] = width,
                    ["height"] = height,
                    ["alpha"] = alpha,
                };
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (rt != null)
                    Object.DestroyImmediate(rt);
                if (texture != null)
                    Object.DestroyImmediate(texture);
            }
        }

        /// <summary>
        /// Relative paths are under the project; absolute ones must point inside it too, so a
        /// caller cannot write a PNG anywhere on disk.
        /// </summary>
        static string ResolveOutputPath(string userPath)
        {
            var path = string.IsNullOrWhiteSpace(userPath) ? "Screenshots/screenshot.png" : userPath.Trim();
            var projectRoot = EditorProjectPaths.GetProjectPath();
            var full = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path));

            var root = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                       Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"'output_path' must be inside the project ({projectRoot}), got '{full}'.");

            return full;
        }
    }
}
