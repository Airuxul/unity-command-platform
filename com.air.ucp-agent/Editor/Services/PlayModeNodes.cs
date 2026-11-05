using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.Services.Objects;
using Air.UcpAgent.Params;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.Services
{
    /// <summary>
    /// Nodes of the running game: every loaded scene and DontDestroyOnLoad, addressed by the paths
    /// <see cref="UiSnapshotService"/> prints.
    /// </summary>
    public static class PlayModeNodes
    {
        static Scene _dontDestroyOnLoad;

        public static void RequirePlayMode(string command)
        {
            if (!EditorApplication.isPlaying)
                throw new UcpException(UcpErrorCodes.PlayModeRequired, $"{command} runs in play mode.", $"Run {CommandNames.Play} first.");
        }

        /// <summary>
        /// Null when no scene has it. Scenes are tried one by one because <see cref="GameObjectPath.Of(Transform)"/>
        /// numbers same-named roots within their own scene.
        /// </summary>
        public static Transform Resolve(string path)
        {
            foreach (var scene in Scenes())
            {
                var node = GameObjectPath.Resolve(Roots(scene), path);
                if (node != null)
                    return node;
            }

            return null;
        }

        public static Transform Require(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException($"Parameter '{ParamKeys.Node}' is required.");

            return Resolve(path) ?? throw GameObjectPath.NotFound(Scenes().SelectMany(Roots).ToList(), path);
        }

        static IEnumerable<Scene> Scenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    yield return scene;
            }

            var dontDestroyOnLoad = DontDestroyOnLoadScene();
            if (dontDestroyOnLoad.IsValid())
                yield return dontDestroyOnLoad;
        }

        static IEnumerable<Transform> Roots(Scene scene) =>
            scene.GetRootGameObjects().Select(go => go.transform);

        /// <summary>SceneManager does not list it; an object moved there is the only way to get hold of it.</summary>
        static Scene DontDestroyOnLoadScene()
        {
            if (_dontDestroyOnLoad.IsValid())
                return _dontDestroyOnLoad;

            var probe = new GameObject("ucp-dont-destroy-on-load-probe");
            UnityEngine.Object.DontDestroyOnLoad(probe);
            _dontDestroyOnLoad = probe.scene;
            UnityEngine.Object.DestroyImmediate(probe);
            return _dontDestroyOnLoad;
        }
    }
}
