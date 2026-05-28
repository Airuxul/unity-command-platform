using System;
using UnityEngine;
using UnityCliConnector.Http;
using UnityCliConnector.Network;

namespace UnityCliConnector
{
    internal sealed class PlayModeHttpEndpoint
    {
        private HttpServer _server;
        private ConnectorListenConfig _listen;
        private readonly ConnectorRequestDispatcher _dispatcher;
        private readonly PlayModeCommandBridge _bridge;
        private readonly string _logLabel;
        private readonly Func<int> _resolvePort;

        public PlayModeHttpEndpoint(
            ICommandHost host,
            string logLabel,
            Func<int> resolvePort)
        {
            _bridge = new PlayModeCommandBridge(host);
            _dispatcher = new ConnectorRequestDispatcher(
                host,
                _bridge,
                new RuntimeCommandStore(host.HostName));
            _logLabel = logLabel;
            _resolvePort = resolvePort;
        }

        public bool IsRunning => _server != null;

        public void Start() =>
            ConnectorHttpLifecycle.TryStart(
                ref _server,
                ref _listen,
                _dispatcher,
                _resolvePort(),
                _logLabel,
                Debug.Log,
                Debug.LogWarning,
                () => _bridge.EnsureStarted());

        public void Stop() => ConnectorHttpLifecycle.Stop(ref _server);
    }
}
