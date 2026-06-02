using Air.UnityConnector.Host;

namespace Air.UnityConnector.Invoke
{
    public static class InvokeAvailability
    {
        public static bool IsAvailableForHost(CommandHostScope scope, string hostKind)
        {
            if (HostKind.IsPlayModeHost(hostKind))
                return scope == CommandHostScope.Runtime || scope == CommandHostScope.Any;

            return scope == CommandHostScope.Editor || scope == CommandHostScope.Any;
        }
    }
}
