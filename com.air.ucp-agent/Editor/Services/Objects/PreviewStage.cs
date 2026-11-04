using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Air.UcpAgent.Editor.Services.Objects
{
    /// <summary>
    /// A throwaway preview scene plus automatic cleanup of whatever was put in it. Anything that
    /// needs a scene to work in — building prefab contents from scratch, rendering a prefab copy —
    /// uses this instead of the scene the user happens to have open.
    /// </summary>
    /// <remarks>Always use with <c>using</c>: Dispose destroys tracked objects and closes the scene.</remarks>
    public sealed class PreviewStage : IDisposable
    {
        readonly List<GameObject> _tracked = new();
        bool _disposed;

        public Scene Scene { get; private set; }

        public static PreviewStage Begin() =>
            new PreviewStage { Scene = EditorSceneManager.NewPreviewScene() };

        /// <summary>Moves an already-created GameObject into the staging scene and tracks it.</summary>
        public GameObject Adopt(GameObject created)
        {
            EditorSceneManager.MoveGameObjectToScene(created, Scene);
            _tracked.Add(created);
            return created;
        }

        /// <summary>Tracks an object already created inside the staging scene.</summary>
        public void Track(GameObject created) => _tracked.Add(created);

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            foreach (var tracked in _tracked.Where(t => t != null))
                Object.DestroyImmediate(tracked);

            if (Scene.IsValid())
                EditorSceneManager.ClosePreviewScene(Scene);
        }
    }
}
