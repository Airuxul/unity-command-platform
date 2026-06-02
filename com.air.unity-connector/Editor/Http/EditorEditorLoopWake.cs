using UnityEditor;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace Air.UnityConnector
{
    /// <summary>Wakes the Editor message loop when CLI traffic arrives (unfocused Editor).</summary>
    internal static class EditorEditorLoopWake
    {
        public static void Force()
        {
            try
            {
                EditorApplication.QueuePlayerLoopUpdate();
            }
            catch
            {
                // ignored
            }

#if UNITY_EDITOR
            try
            {
                InternalEditorUtility.RepaintAllViews();
            }
            catch
            {
                // ignored
            }
#endif
        }
    }
}
