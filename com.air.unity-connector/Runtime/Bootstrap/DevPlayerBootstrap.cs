using UnityEngine;

namespace Air.UnityConnector
{
    /// <summary>Starts player HTTP in Development Build players (excluded from Release).</summary>
    public static class DevPlayerBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init() => PlayerHttpHost.Start();
    }
}
