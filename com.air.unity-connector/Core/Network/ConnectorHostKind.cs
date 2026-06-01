namespace UnityCliConnector.Network
{
    /// <summary>Values returned by GET /health; stored in CLI profile <c>connector_host</c>.</summary>
    public static class ConnectorHostKind
    {
        public const string Editor = "editor";
        public const string EditorPlay = "editor_play";
        public const string Player = "player";

        public static bool IsPlayModeHost(string hostKind) =>
            hostKind is EditorPlay or Player;
    }
}
