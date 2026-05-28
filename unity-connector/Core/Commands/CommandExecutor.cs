using System;
using System.Collections.Generic;

namespace UnityCliConnector
{
    /// <summary>Shared command execution for editor and play-mode hosts.</summary>
    public static class CommandExecutor
    {
        public static void ExecuteCommand(CommandContext context, Dictionary<string, object> parameters)
        {
            var handler = CommandDiscovery.FindForHost(context.Command, context.HostName);

            if (handler == null)
                throw new InvalidOperationException($"Unknown command: {context.Command}");

            handler.ExecuteCommand(context, parameters);
        }
    }
}
