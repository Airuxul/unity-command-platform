using System.Collections.Generic;
using Air.UcpAgent.Cli;

namespace Air.UcpAgent.Invoke
{
    /// <summary>Default <see cref="IInvokeRegistry"/> backed by <see cref="CliCommandDiscovery"/>.</summary>
    public sealed class DiscoveryInvokeRegistry : IInvokeRegistry
    {
        public static readonly DiscoveryInvokeRegistry Instance = new();

        public IReadOnlyList<IInvokeHandler> Handlers => CliCommandDiscovery.Handlers;

        public IInvokeHandler Find(string command, string hostKind) =>
            CliCommandDiscovery.FindForHost(command, hostKind);
    }
}
