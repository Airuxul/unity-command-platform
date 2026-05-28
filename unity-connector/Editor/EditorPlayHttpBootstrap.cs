using UnityEditor;

namespace UnityCliConnector
{
    /// <summary>Starts/stops <see cref="EditorPlayHttpHost"/> with Editor Play Mode.</summary>
    [InitializeOnLoad]
    internal static class EditorPlayHttpBootstrap
    {
        static EditorPlayHttpBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (EditorApplication.isPlaying)
                EditorPlayHttpHost.Start();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                EditorPlayHttpHost.Start();
            else if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.EnteredEditMode)
                EditorPlayHttpHost.Stop();
        }
    }
}
