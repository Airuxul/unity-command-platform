using Air.UcpAgent.Runtime;
using UnityEditor;

namespace Air.UcpAgent.Editor
{
    [InitializeOnLoad]
    static class RuntimePlayModeLifecycle
    {
        static RuntimePlayModeLifecycle()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode)
                RuntimeAgentHost.ShutdownForPlayModeExit();
        }
    }
}
