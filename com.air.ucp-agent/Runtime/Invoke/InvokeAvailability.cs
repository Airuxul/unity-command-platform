using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Invoke
{
    public static class InvokeAvailability
    {
        public static bool IsAvailableForHost(CommandHostScope scope, string hostKind)
        {
            if (string.IsNullOrEmpty(hostKind))
                return true;

            return hostKind switch
            {
                "editor" => scope is CommandHostScope.Editor or CommandHostScope.Any,
                "runtime" => scope is CommandHostScope.Runtime or CommandHostScope.Any,
                _ => scope is CommandHostScope.Any,
            };
        }
    }
}
