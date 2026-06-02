using Air.UnityConnector.Host;

namespace Air.UnityConnector
{
    /// <summary>HTTP for Unity Editor Play Mode (<c>host=editor_play</c>, port 6794).</summary>
    public static class EditorPlayHttpHost
    {
        private static readonly PlayConnectorServer Server = new(
            HostKind.EditorPlay,
            "Editor Play HTTP server",
            HostNetwork.ResolveEditorPlayPort);

        public static bool IsRunning => Server.IsListening;

        public static void Start() => Server.Start();

        public static void Stop() => Server.Stop();

        public static void StopForAssemblyReload() => Server.StopForAssemblyReload();

        public static void CleanupStaleBridges() => Server.CleanupStaleBridgesNow();
    }

    /// <summary>HTTP for Development Build players (<c>host=player</c>, port 6795).</summary>
    public static class PlayerHttpHost
    {
        private static readonly PlayConnectorServer Server = new(
            HostKind.Player,
            "Player HTTP server",
            HostNetwork.ResolvePlayerPort);

        public static bool IsRunning => Server.IsListening;

        public static void Start() => Server.Start();

        public static void Stop() => Server.Stop();
    }
}
