using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Protocol;
using UnityEngine;

namespace Air.UcpAgent.Runtime
{
    public static class RuntimeCommandService
    {
        public static UcpResult Execute(UcpCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.id))
                return UcpResults.Failure(command?.id, "invalid_command", "Command id is required");

            if (!Application.isPlaying)
                return UcpResults.Failure(command.id, "not_in_play_mode", "Runtime commands require Play Mode");

            // No job store at runtime: a command that does not finish synchronously fails.
            return CommandExecutor.Execute(CommandRegistry.RuntimeHost, command, jobs: null);
        }

        /// <summary>Names only: the runtime agent has never advertised aliases.</summary>
        public static List<string> BuildCapabilities() =>
            CommandRegistry.Capabilities(CommandRegistry.RuntimeHost, includeAliases: false);
    }
}
