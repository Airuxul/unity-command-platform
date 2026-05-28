using UnityEditor;

namespace UnityCliConnector
{
    /// <summary>Cached play flag for HTTP threads (never call <see cref="EditorApplication.isPlaying"/> off main thread).</summary>
    [InitializeOnLoad]
    public static class EditorPlayState
    {
        public static volatile bool IsPlaying;

        static EditorPlayState()
        {
            EditorApplication.playModeStateChanged += _ => Refresh();
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh() => IsPlaying = EditorApplication.isPlaying;
    }
}
