namespace Air.UnityConnector.Host
{
    public static class HostKind
    {
        public const string Editor = "editor";
        public const string EditorPlay = "editor_play";
        public const string Player = "player";

        public static bool IsPlayModeHost(string hostKind) =>
            hostKind is EditorPlay or Player;
    }
}
