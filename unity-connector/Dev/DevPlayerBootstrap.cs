using UnityEngine;

namespace UnityCliConnector
{
    /// <summary>Starts player HTTP in Development Build players (Dev assembly excluded from Release).</summary>
    public static class DevPlayerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() => PlayerHttpHost.Start();
    }
}
