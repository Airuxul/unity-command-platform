using System.Collections.Generic;

namespace Air.UnityConnector.Http
{
    /// <summary>Optional host-specific fields for GET /health.</summary>
    public interface IHealthMetadataProvider
    {
        void AppendHealth(Dictionary<string, object> payload);
    }
}
