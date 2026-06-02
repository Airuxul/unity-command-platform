using UnityEditor;

namespace Air.UnityConnector
{
    /// <summary>Cached Editor flags safe to read from HTTP worker threads.</summary>
    [InitializeOnLoad]
    public static class EditorPlayState
    {
        public static volatile bool IsPlaying;
        public static volatile bool IsPaused;
        public static volatile bool IsCompiling;
        public static volatile bool IsUpdating;

        static EditorPlayState()
        {
            EditorApplication.playModeStateChanged += _ => Refresh();
            EditorApplication.update += Refresh;
            EditorApplication.delayCall += Refresh;
        }

        private static void Refresh()
        {
            IsPlaying = EditorApplication.isPlaying;
            IsPaused = EditorApplication.isPlaying && EditorApplication.isPaused;
            IsCompiling = EditorApplication.isCompiling;
            IsUpdating = EditorApplication.isUpdating;
        }
    }
}
