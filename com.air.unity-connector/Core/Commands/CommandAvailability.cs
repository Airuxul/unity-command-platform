using UnityCliConnector.Network;

namespace UnityCliConnector
{
    /// <summary>Shared rules for <see cref="CommandRouter"/> and <see cref="CommandCatalog"/>.</summary>
    public static class CommandAvailability
    {
        public static bool IsAvailableForHost(CommandScope scope, string hostKind)
        {
            if (ConnectorHostKind.IsPlayModeHost(hostKind))
                return scope == CommandScope.Runtime || scope == CommandScope.Any;

            return scope == CommandScope.Editor || scope == CommandScope.Any;
        }
    }
}
