using System;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Editor.IO;
using UnityEngine.SceneManagement;

namespace Air.UcpAgent.Editor.Services.Scenes
{
    /// <summary>
    /// The <c>scene</c> argument, resolved in one place: a loaded scene's name or asset path, or the
    /// active scene when omitted.
    /// </summary>
    public static class SceneLookup
    {
        public static Scene Require(string nameOrPath)
        {
            if (string.IsNullOrWhiteSpace(nameOrPath))
            {
                var active = SceneManager.GetActiveScene();
                if (!active.IsValid())
                    throw new InvalidOperationException($"No scene is loaded. Open one with {CommandNames.SceneOpen}.");

                return active;
            }

            var wanted = AssetPaths.Normalize(nameOrPath);
            var loaded = new List<string>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                loaded.Add(string.IsNullOrEmpty(scene.path) ? scene.name : scene.path);
                if (string.Equals(scene.name, wanted, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(AssetPaths.Normalize(scene.path), wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return scene;
                }
            }

            throw new ArgumentException(
                $"Scene '{nameOrPath}' is not loaded. Loaded scenes: {(loaded.Count == 0 ? "(none)" : string.Join(", ", loaded))}.");
        }
    }
}
