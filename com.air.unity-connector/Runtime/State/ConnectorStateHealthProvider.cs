using System;
using System.Collections.Generic;
using Air.UnityConnector.Http;

namespace Air.UnityConnector.State
{
    /// <summary>GET /health metadata from a snapshot supplier (HTTP or local-file path).</summary>
    public sealed class ConnectorStateHealthProvider : IHealthMetadataProvider
    {
        readonly Func<ConnectorStateSnapshot> _capture;
        readonly Action<Dictionary<string, object>, ConnectorStateSnapshot> _appendExtra;

        public ConnectorStateHealthProvider(
            Func<ConnectorStateSnapshot> capture,
            Action<Dictionary<string, object>, ConnectorStateSnapshot> appendExtra = null)
        {
            _capture = capture ?? throw new ArgumentNullException(nameof(capture));
            _appendExtra = appendExtra;
        }

        public void AppendHealth(Dictionary<string, object> payload)
        {
            var snapshot = _capture();
            snapshot.AppendHealthFields(payload);
            _appendExtra?.Invoke(payload, snapshot);
        }
    }
}
