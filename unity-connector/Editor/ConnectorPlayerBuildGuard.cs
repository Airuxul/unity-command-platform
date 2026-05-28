using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace UnityCliConnector
{
    /// <summary>
    /// Reminds that player HTTP bootstrap is only included in Development Build players.
    /// </summary>
    public sealed class ConnectorPlayerBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (EditorUserBuildSettings.development)
            {
                Debug.Log(
                    "[unity-connector] Development Build: player HTTP bootstrap will be included (UnityCliConnector.Runtime).");
                return;
            }

            Debug.Log(
                "[unity-connector] Release Build: runtime bootstrap is excluded (by DEVELOPMENT_BUILD) — no player HTTP. Editor tools unchanged.");
        }
    }
}
