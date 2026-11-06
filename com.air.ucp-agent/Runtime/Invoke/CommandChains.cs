using System;
using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Protocol;

namespace Air.UcpAgent.Invoke
{
    public static class CommandChains
    {
        /// <summary>Null when no command on this host names a before or after command.</summary>
        public static Dictionary<string, CommandChain> Publish(string host)
        {
            Dictionary<string, CommandChain> map = null;
            foreach (var entry in CommandRegistry.ForHost(host))
            {
                var before = entry.Descriptor.Before ?? Array.Empty<string>();
                var after = entry.Descriptor.After ?? Array.Empty<string>();
                if (before.Length == 0 && after.Length == 0)
                    continue;

                map ??= new Dictionary<string, CommandChain>(StringComparer.OrdinalIgnoreCase);
                var chain = new CommandChain
                {
                    before = new List<string>(before),
                    after = new List<string>(after),
                };
                map[entry.Name] = chain;
                foreach (var alias in entry.Aliases)
                {
                    if (!string.IsNullOrEmpty(alias))
                        map[alias] = chain;
                }
            }

            return map;
        }
    }
}
