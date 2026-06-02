namespace Air.UnityConnector.Host
{
    public static class HostPorts
    {
        public const string EnvEditorPort = "UNITY_CMD_PORT";
        public const string EnvEditorPlayPort = "UNITY_CMD_EDITOR_PLAY_PORT";
        public const string EnvPlayerPort = "UNITY_CMD_PLAYER_PORT";

        public const int DefaultEditor = 6547;
        public const int DefaultEditorPlay = 6794;
        public const int DefaultPlayer = 6795;
    }
}
