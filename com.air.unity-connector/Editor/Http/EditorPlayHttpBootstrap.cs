using UnityEditor;

namespace Air.UnityConnector
{
    /// <summary>Starts/stops <see cref="EditorPlayHttpHost"/> with Editor Play Mode.</summary>
    [InitializeOnLoad]
    internal static class EditorPlayHttpBootstrap
    {
        private static bool _stoppedForReload;

        static EditorPlayHttpBootstrap()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorPlayHttpHost.CleanupStaleBridges();
            if (EditorApplication.isPlaying)
                EditorPlayHttpHost.Start();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ScheduleStart();
                return;
            }

            if (state is PlayModeStateChange.ExitingPlayMode or PlayModeStateChange.EnteredEditMode)
                ScheduleStopAndCleanup();
        }

        private static void OnBeforeAssemblyReload()
        {
            if (_stoppedForReload)
                return;

            _stoppedForReload = true;
            EditorPlayHttpHost.StopForAssemblyReload();
            EditorPlayHttpHost.CleanupStaleBridges();
        }

        private static void StartAfterPlayEntered()
        {
            _stoppedForReload = false;
            EditorPlayHttpHost.Start();
        }

        private static void ScheduleStart()
        {
            // Defer HTTP lifecycle off the PlayMode callback to avoid blocking Enter/Exit Play.
            EditorApplication.delayCall += StartAfterPlayEntered;
        }

        private static void ScheduleStopAndCleanup()
        {
            // Defer HTTP lifecycle off the PlayMode callback to avoid blocking Enter/Exit Play.
            EditorApplication.delayCall += StopAndCleanup;
        }

        private static void StopAndCleanup()
        {
            EditorPlayHttpHost.Stop();
            EditorPlayHttpHost.CleanupStaleBridges();
        }
    }
}
