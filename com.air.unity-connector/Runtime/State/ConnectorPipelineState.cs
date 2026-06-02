namespace Air.UnityConnector.State
{
    /// <summary>Connector / command-pipeline lifecycle (not Unity Play Mode).</summary>
    public static class ConnectorPipelineState
    {
        public const string Ready = "ready";
        public const string Reloading = "reloading";
        public const string Compiling = "compiling";
        public const string Refreshing = "refreshing";
        public const string Stopped = "stopped";
        public const string EnteringPlayMode = "entering_playmode";
    }
}
