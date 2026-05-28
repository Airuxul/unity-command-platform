using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityCliConnector
{
    /// <summary>
    /// Reminds that unity-connector Dev player HTTP is only included in Development Build players.
    /// </summary>
    public sealed class ConnectorPlayerBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.development)
            {
                Debug.Log(
                    "[unity-connector] Development Build: Dev player HTTP will be included (UnityCliConnector.Dev).");
                return;
            }

            Debug.Log(
                "[unity-connector] Release Build: UnityCliConnector.Dev is excluded — no player HTTP. Editor tools unchanged.");
        }
    }
}
