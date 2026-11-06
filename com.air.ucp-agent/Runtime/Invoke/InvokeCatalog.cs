using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Params;
using Newtonsoft.Json;

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
                commands.Add(Describe(entry));

                versionParts.Add(
                    $"{entry.Name}|{entry.Completion}|{string.Join(",", entry.Aliases)}|{entry.DefaultTimeoutMs}|{string.Join("|", paramDescs)}|{string.Join(",", entry.Descriptor.Before ?? Array.Empty<string>())}|{string.Join(",", entry.Descriptor.After ?? Array.Empty<string>())}");

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
                // Command classes that did not load, with why. Empty when every one is well-formed.
                ["load_errors"] = CommandRegistry.LoadErrors.Concat(FunctionRegistry.LoadErrors).ToList(),
            };
        }

        /// <summary>One command's card: its catalog entry plus an example call and the error codes.</summary>
        public static Dictionary<string, object> BuildCard(string host, string nameOrAlias)
        {
            var entry = Require(host, nameOrAlias);
            var card = Describe(entry);
            card["example"] = Example(entry);
            card["errors"] = UcpErrorCodes.All.Select(e => new Dictionary<string, object>
            {
                ["code"] = e.code,
                ["meaning"] = e.meaning,
            }).ToList();
            return new Dictionary<string, object> { ["ok"] = true, ["command"] = card };
        }

        /// <summary>The cards as markdown: one command, or every command, then the error codes once.</summary>
        public static Dictionary<string, object> BuildMarkdown(string host, string nameOrAlias)
        {
            var entries = string.IsNullOrWhiteSpace(nameOrAlias)
                ? CommandRegistry.ForHost(host).ToList()
                : new List<CommandEntry> { Require(host, nameOrAlias) };

            var md = new StringBuilder();
            foreach (var entry in entries)
                AppendCard(md, entry);

            Line(md, "## Error codes");
            Line(md);
            Line(md, "A failed command returns one of these as `error_code`, the reason as `message`, and what to try next as `hint`.");
            Line(md);
            Line(md, "| code | meaning |");
            Line(md, "|---|---|");
            foreach (var (code, meaning) in UcpErrorCodes.All)
                Line(md, $"| `{code}` | {meaning} |");

            return new Dictionary<string, object> { ["ok"] = true, ["markdown"] = md.ToString() };
        }

        static Dictionary<string, object> Describe(CommandEntry entry) =>
            new()
            {
                ["name"] = entry.Name,
                ["scope"] = entry.Scope.ToString().ToLowerInvariant(),
                ["description"] = entry.Description,
                ["completion"] = entry.Completion,
                ["aliases"] = entry.Aliases,
                ["default_timeout_ms"] = entry.DefaultTimeoutMs > 0 ? entry.DefaultTimeoutMs : (object)null,
                ["params"] = entry.ParamDescriptions ?? Array.Empty<string>(),
                ["before"] = entry.Descriptor.Before ?? Array.Empty<string>(),
                ["after"] = entry.Descriptor.After ?? Array.Empty<string>(),
            };

        static CommandEntry Require(string host, string nameOrAlias)
        {
            var entry = CommandRegistry.Find(host, nameOrAlias);
            if (entry != null)
                return entry;

            var wanted = nameOrAlias?.Trim() ?? "";
            var similar = CommandRegistry.ForHost(host)
                .Select(e => e.Name)
                .Where(n => n.IndexOf(wanted, StringComparison.OrdinalIgnoreCase) >= 0
                    || wanted.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(8)
                .ToArray();
            throw new UcpException(
                UcpErrorCodes.UnknownCommand,
                $"No command '{wanted}' on this host.",
                similar.Length > 0
                    ? $"Did you mean: {string.Join(", ", similar)}?"
                    : $"Run {CommandNames.Help} without 'command' to list them all.");
        }

        static void AppendCard(StringBuilder md, CommandEntry entry)
        {
            Line(md, $"## {entry.Name}");
            Line(md);
            if (!string.IsNullOrEmpty(entry.Description))
            {
                Line(md, entry.Description);
                Line(md);
            }

            var facts = new List<string>();
            if (entry.Aliases.Length > 0)
                facts.Add("aliases: " + string.Join(", ", entry.Aliases.Select(a => $"`{a}`")));
            if (entry.Completion != null)
                facts.Add($"deferred until `{entry.Completion}`");
            if (entry.DefaultTimeoutMs > 0)
                facts.Add($"default timeout {entry.DefaultTimeoutMs} ms");
            if (entry.Descriptor.Before is { Length: > 0 })
                facts.Add("before: " + string.Join(", ", entry.Descriptor.Before.Select(name => $"`{name}`")));
            if (entry.Descriptor.After is { Length: > 0 })
                facts.Add("after: " + string.Join(", ", entry.Descriptor.After.Select(name => $"`{name}`")));
            if (facts.Count > 0)
            {
                Line(md, string.Join(" · ", facts));
                Line(md);
            }

            var lines = entry.ParamDescriptions ?? Array.Empty<string>();
            foreach (var line in lines)
            {
                var flag = line.IndexOf(' ');
                Line(md, flag < 0 ? $"- `{line}`" : $"- `{line.Substring(0, flag)}` {line.Substring(flag + 1)}");
            }
            if (lines.Length > 0)
                Line(md);

            Line(md, "```");
            Line(md, Example(entry));
            Line(md, "```");
            Line(md);
        }

        /// <summary>AppendLine writes \r\n on Windows; the markdown uses \n everywhere.</summary>
        static void Line(StringBuilder md, string text = "") => md.Append(text).Append('\n');

        /// <summary>A call with every required parameter filled in by a placeholder of its type.</summary>
        static string Example(CommandEntry entry)
        {
            var args = new Dictionary<string, object>();
            foreach (var param in UcpParamBinder.Inspect(entry.ParamType).Where(p => p.Required))
                args[param.Key] = Placeholder(param);

            var call = $"ucp-cli run {entry.Name}";
            return args.Count == 0 ? call : $"{call} --args '{JsonConvert.SerializeObject(args)}'";
        }

        static object Placeholder(UcpParamInfo param)
        {
            if (!string.IsNullOrEmpty(param.AllowedValues))
                return param.AllowedValues.Split('|')[0];

            return param.TypeName switch
            {
                "bool" => true,
                "int" => 1,
                "float" => 1.0,
                "string[]" => new[] { $"<{param.Key}>" },
                _ when param.TypeName.StartsWith("list", StringComparison.Ordinal) => Array.Empty<object>(),
                _ => $"<{param.Key}>",
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
