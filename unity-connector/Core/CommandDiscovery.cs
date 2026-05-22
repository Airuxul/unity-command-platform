using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace UnityCliConnector
{
    public static class CommandDiscovery
    {
        private static List<ICommandHandler> _handlers;
        private static readonly object Gate = new();

        public static IReadOnlyList<ICommandHandler> Handlers
        {
            get
            {
                EnsureLoaded();
                return _handlers;
            }
        }

        public static void Invalidate() => _handlers = null;

        public static ICommandHandler Find(string command)
        {
            EnsureLoaded();
            return _handlers.FirstOrDefault(h =>
                string.Equals(h.Name, command, StringComparison.OrdinalIgnoreCase));
        }

        private static void EnsureLoaded()
        {
            if (_handlers != null)
                return;

            lock (Gate)
            {
                if (_handlers != null)
                    return;

                var list = new List<ICommandHandler>();
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try
                    {
                        types = assembly.GetTypes();
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        types = ex.Types.Where(t => t != null).ToArray();
                    }

                    foreach (var type in types)
                    {
                        if (type == null)
                            continue;

                        var attr = type.GetCustomAttribute<CliCommandAttribute>();
                        if (attr == null)
                            continue;

                        var method = type.GetMethod(
                            "Run",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        if (method == null)
                            continue;

                        list.Add(new ReflectiveHandler(attr, method));
                    }
                }

                _handlers = list
                    .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private static string[] ParseAliases(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Array.Empty<string>();

            return raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(a => a.Trim())
                .Where(a => a.Length > 0)
                .ToArray();
        }

        private sealed class ReflectiveHandler : ICommandHandler
        {
            private readonly CliCommandAttribute _attr;
            private readonly MethodInfo _method;
            private readonly string[] _aliases;

            public ReflectiveHandler(CliCommandAttribute attr, MethodInfo method)
            {
                _attr = attr;
                _method = method;
                _aliases = ParseAliases(attr.Aliases);
            }

            public string Name => _attr.Name;
            public CommandScope Scope => _attr.Scope;
            public string Description => _attr.Description ?? "";
            public bool IsJob => _attr.IsJob;
            public string Completion => _attr.Completion ?? "";
            public string[] Aliases => _aliases;
            public int DefaultTimeoutMs => _attr.DefaultTimeoutMs;
            public bool AllowConnectionRetry => _attr.AllowConnectionRetry;

            public CommandResult Execute(CliParams parameters)
            {
                try
                {
                    var result = _method.Invoke(null, new object[] { parameters });
                    if (result is CommandResult cr)
                        return cr;
                    return CommandResult.Success(result);
                }
                catch (TargetInvocationException ex)
                {
                    return CommandResult.Fail(ex.InnerException?.Message ?? ex.Message);
                }
                catch (Exception ex)
                {
                    return CommandResult.Fail(ex.Message);
                }
            }
        }
    }
}
