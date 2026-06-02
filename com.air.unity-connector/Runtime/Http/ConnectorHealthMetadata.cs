using System.Collections.Generic;

namespace Air.UnityConnector.Http
{
    /// <summary>Adds <c>connector_build</c> and optional host-specific health fields.</summary>
    public sealed class ConnectorHealthMetadata : IHealthMetadataProvider
    {
        public static readonly ConnectorHealthMetadata Default = new(null);

        readonly IHealthMetadataProvider _inner;

        public ConnectorHealthMetadata(IHealthMetadataProvider inner) => _inner = inner;

        public void AppendHealth(Dictionary<string, object> payload)
        {
            payload["connector_build"] = ConnectorBuild.Id;
            _inner?.AppendHealth(payload);
        }
    }
}
