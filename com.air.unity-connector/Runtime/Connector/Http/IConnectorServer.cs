namespace Air.UnityConnector.Http
{
    /// <summary>
    /// Unified HTTP command server lifecycle (Editor edit-mode and Play-mode hosts).
    /// </summary>
    public interface IConnectorServer
    {
        string HostName { get; }

        bool IsListening { get; }

        void Start();

        void Stop();
    }
}
