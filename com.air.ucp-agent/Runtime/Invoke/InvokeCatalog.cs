using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Air.UcpAgent.Cli;

namespace Air.UcpAgent.Invoke
{
    /// <summary>The <c>help</c> payload: every command a host runs, generated from the registry.</summary>
    public static class InvokeCatalog
    {
        public static Dictionary<string, object> BuildResponse(string host)
        {
            var commands = new List<Dictionary<string, object>>();
            var versionParts = new List<string>();
            var aliasToCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in CommandRegistry.ForHost(host))
            {
                var paramDescs = entry.ParamDescriptions ?? Array.Empty<string>();
                commands.Add(new Dictionary<string, object>
                {
                    ["name"] = entry.Name,
                    ["scope"] = entry.Scope.ToString().ToLowerInvariant(),
                    ["description"] = entry.Description,
                    ["completion"] = entry.Completion,
                    ["aliases"] = entry.Aliases,
                    ["default_timeout_ms"] = entry.DefaultTimeoutMs > 0 ? entry.DefaultTimeoutMs : (object)null,
                    ["allow_connection_retry"] = entry.AllowConnectionRetry,
                    ["params"] = paramDescs,
                });

                versionParts.Add(
                    $"{entry.Name}|{entry.Completion}|{string.Join(",", entry.Aliases)}|{entry.DefaultTimeoutMs}|{string.Join("|", paramDescs)}");

                foreach (var alias in entry.Aliases)
                {
                    if (!string.IsNullOrEmpty(alias))
                        aliasToCommand[alias] = entry.Name;
                }
            }

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["catalog_version"] = ComputeVersion(versionParts),
                ["commands"] = commands,
                ["alias_to_command"] = aliasToCommand,
            };
        }

        static string ComputeVersion(IEnumerable<string> parts)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", parts)));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant().Substring(0, 12);
        }
    }
}
