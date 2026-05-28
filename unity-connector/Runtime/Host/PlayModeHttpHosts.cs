using UnityCliConnector.Network;

namespace UnityCliConnector
{
    /// <summary>HTTP for Unity Editor Play Mode (<c>host=editor_play</c>, port 6794).</summary>
    public static class EditorPlayHttpHost
    {
        private static readonly PlayModeHttpEndpoint Endpoint = new(
            new PlayModeCommandHost(ConnectorHostKind.EditorPlay),
            "Editor Play HTTP",
            ConnectorNetwork.ResolveEditorPlayPort);

        public static bool IsRunning => Endpoint.IsRunning;

        public static void Start() => Endpoint.Start();

        public static void Stop() => Endpoint.Stop();
    }

    /// <summary>HTTP for Development Build players (<c>host=player</c>, port 6795).</summary>
    public static class PlayerHttpHost
    {
        private static readonly PlayModeHttpEndpoint Endpoint = new(
            new PlayModeCommandHost(ConnectorHostKind.Player),
            "Player HTTP",
            ConnectorNetwork.ResolvePlayerPort);

        public static bool IsRunning => Endpoint.IsRunning;

        public static void Start() => Endpoint.Start();

        public static void Stop() => Endpoint.Stop();
    }
}
