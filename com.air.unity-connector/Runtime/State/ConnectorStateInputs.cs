namespace Air.UnityConnector.State
{
    /// <summary>Thread-safe inputs for <see cref="ConnectorStateEvaluator"/>.</summary>
    public sealed class ConnectorStateInputs
    {
        public string ForcedConnectorState;
        public bool ListenerActive = true;
        public bool DomainReloading;
        public bool IsCompiling;
        public bool IsUpdating;
        public bool IsPlaying;
        public bool IsPaused;
        public bool IsCommandReady = true;
    }
}
