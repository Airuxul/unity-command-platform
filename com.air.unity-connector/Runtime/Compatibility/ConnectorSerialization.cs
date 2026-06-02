// @tag cli-param
using Air.UnityGameCore.Runtime.Serialization;

namespace Air.UnityConnector
{
    /// <summary>
    /// Ensures <see cref="JsonSerialization.Instance"/> before connector / game-core HTTP uses JSON.
    /// </summary>
    public static class ConnectorSerialization
    {
        public static void EnsureRegistered()
        {
            if (JsonSerialization.Instance != null)
                return;

            JsonSerializationBootstrap.EnsureRegistered();

            if (JsonSerialization.Instance == null)
                JsonSerialization.Instance = NewtonsoftJsonSerializer.Default;
        }
    }
}
