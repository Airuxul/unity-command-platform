using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace UnityCliConnector
{
    public static class CommandCatalog
    {
        public static Dictionary<string, object> BuildResponse()
        {
            var commands = new List<Dictionary<string, object>>();
            var aliasToCommand = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var versionParts = new List<string>();

            foreach (var handler in CommandDiscovery.Handlers)
            {
                var completion = CommandJobCatalog.GetCompletionKind(handler.Name, null);
                var isJob = !string.IsNullOrEmpty(completion);
                var aliases = handler.Aliases ?? Array.Empty<string>();

                var entry = new Dictionary<string, object>
                {
                    ["name"] = handler.Name,
                    ["scope"] = handler.Scope.ToString().ToLowerInvariant(),
                    ["description"] = handler.Description ?? "",
                    ["is_job"] = isJob,
                    ["completion"] = completion,
                    ["aliases"] = aliases,
                    ["default_timeout_ms"] = handler.DefaultTimeoutMs > 0 ? handler.DefaultTimeoutMs : (object)null,
                    ["allow_connection_retry"] = handler.AllowConnectionRetry,
                };
                commands.Add(entry);

                versionParts.Add($"{handler.Name}|{isJob}|{completion}|{string.Join(",", aliases)}|{handler.DefaultTimeoutMs}");

                foreach (var alias in aliases)
                {
                    if (!string.IsNullOrEmpty(alias))
                        aliasToCommand[alias] = handler.Name;
                }
            }

            var version = ComputeVersion(versionParts);

            return new Dictionary<string, object>
            {
                ["ok"] = true,
                ["catalog_version"] = version,
                ["commands"] = commands,
                ["alias_to_command"] = aliasToCommand,
            };
        }

        private static string ComputeVersion(IEnumerable<string> parts)
        {
            var joined = string.Join("\n", parts);
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(joined));
            var hex = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return hex.Substring(0, 12);
        }
    }
}
