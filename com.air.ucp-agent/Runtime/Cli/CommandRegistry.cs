// @tag cli-command
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;

namespace Air.UcpAgent.Cli
{
    /// <summary>One discovered command type and what its descriptor declares.</summary>
    public sealed class CommandEntry
    {
        string[] _paramDescriptions;

        public CommandEntry(InvokeDescriptor descriptor, Type commandType)
        {
            Descriptor = descriptor;
            CommandType = commandType;
            ParamType = descriptor.ParamType ?? FindParamType(commandType);
        }

        public InvokeDescriptor Descriptor { get; }
        public Type CommandType { get; }

        /// <summary>Null for a command without parameters.</summary>
        public Type ParamType { get; }

        public string Name => Descriptor.Name;
        public CommandHostScope Scope => Descriptor.Scope;
        public string Description => Descriptor.Description ?? "";
        public string[] Aliases => Descriptor.Aliases ?? Array.Empty<string>();
        public int DefaultTimeoutMs => Descriptor.DefaultTimeoutMs;
        public bool AllowConnectionRetry => Descriptor.AllowConnectionRetry;

        /// <summary>The completion kind a deferred command declares; null for a synchronous one.</summary>
        public string Completion => Descriptor is DeferredInvokeDescriptor deferred ? deferred.Completion : null;

        public string[] ParamDescriptions => _paramDescriptions ??= CliParamBinder.Describe(ParamType);

        public bool RunsOn(string host) =>
            Scope == CommandHostScope.Any
            || host == CommandRegistry.EditorHost && Scope == CommandHostScope.Editor
            || host == CommandRegistry.RuntimeHost && Scope == CommandHostScope.Runtime;

        public CliCommand Create() => (CliCommand)Activator.CreateInstance(CommandType);

        static Type FindParamType(Type commandType)
        {
            for (var type = commandType; type != null && type != typeof(object); type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(CliCommand<>))
                    return type.GetGenericArguments()[0];
            }

            return null;
        }
    }

    /// <summary>
    /// Every <see cref="CliCommand"/> in the loaded assemblies, found once per domain. Lookups are
    /// per host: the Editor and the runtime agent each see only the commands that run on them, so
    /// the two <c>help</c> commands never shadow each other and the runtime port cannot reach an
    /// Editor-only command.
    /// </summary>
    public static class CommandRegistry
    {
        public const string EditorHost = "editor";
        public const string RuntimeHost = "runtime";

        static readonly object Gate = new();
        static IReadOnlyList<CommandEntry> _entries;
        static readonly Dictionary<string, Dictionary<string, CommandEntry>> ByHost = new();

        public static IReadOnlyList<CommandEntry> Entries
        {
            get
            {
                EnsureLoaded();
                return _entries;
            }
        }

        /// <summary>Commands that run on <paramref name="host"/>, ordered by name.</summary>
        public static IEnumerable<CommandEntry> ForHost(string host) => Entries.Where(e => e.RunsOn(host));

        /// <summary>Resolves a command name or alias on <paramref name="host"/>; null if there is none.</summary>
        public static CommandEntry Find(string host, string nameOrAlias)
        {
            if (string.IsNullOrWhiteSpace(nameOrAlias))
                return null;

            EnsureLoaded();
            return Lookup(host).TryGetValue(nameOrAlias.Trim(), out var entry) ? entry : null;
        }

        /// <summary>
        /// Command types the host advertises. ucp-host rejects a type that is not in the session's
        /// capability list before it reaches the agent, so an alias only dispatches if it is listed.
        /// </summary>
        public static List<string> Capabilities(string host, bool includeAliases)
        {
            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in ForHost(host))
            {
                if (seen.Add(entry.Name))
                    list.Add(entry.Name);
                if (!includeAliases)
                    continue;

                foreach (var alias in entry.Aliases)
                {
                    if (!string.IsNullOrEmpty(alias) && seen.Add(alias))
                        list.Add(alias);
                }
            }

            return list;
        }

        static Dictionary<string, CommandEntry> Lookup(string host)
        {
            lock (Gate)
            {
                if (ByHost.TryGetValue(host ?? "", out var map))
                    return map;

                map = new Dictionary<string, CommandEntry>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in ForHost(host))
                {
                    foreach (var key in new[] { entry.Name }.Concat(entry.Aliases))
                    {
                        if (string.IsNullOrEmpty(key))
                            continue;
                        if (map.TryGetValue(key, out var existing))
                        {
                            UcpLog.LogWarning(
                                $"[ucp-agent] '{key}' is declared by both {existing.CommandType.FullName} and {entry.CommandType.FullName}; keeping the first.");
                            continue;
                        }

                        map[key] = entry;
                    }
                }

                ByHost[host ?? ""] = map;
                return map;
            }
        }

        static void EnsureLoaded()
        {
            if (_entries != null)
                return;

            lock (Gate)
            {
                if (_entries != null)
                    return;

                var list = new List<CommandEntry>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    foreach (var type in LoadableTypes(assembly))
                    {
                        if (!type.IsClass || type.IsAbstract || type.ContainsGenericParameters
                            || !typeof(CliCommand).IsAssignableFrom(type))
                        {
                            continue;
                        }

                        try
                        {
                            var descriptor = ((CliCommand)Activator.CreateInstance(type)).Descriptor;
                            if (descriptor != null && !string.IsNullOrEmpty(descriptor.Name))
                                list.Add(new CommandEntry(descriptor, type));
                        }
                        catch (Exception ex)
                        {
                            UcpLog.LogWarning(
                                $"[ucp-agent] CLI command type '{type.FullName}' cannot be instantiated: {Unwrap(ex).Message}");
                        }
                    }
                }

                _entries = list
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.CommandType.FullName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        static IEnumerable<Type> LoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

        public static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException { InnerException: not null } tie ? tie.InnerException : ex;
    }
}
