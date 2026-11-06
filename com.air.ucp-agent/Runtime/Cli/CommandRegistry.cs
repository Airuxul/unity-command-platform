// @tag cli-command
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Params;
using Newtonsoft.Json;

namespace Air.UcpAgent.Cli
{
    /// <summary>One discovered command class: its descriptor, and how to call its Execute and Undo.</summary>
    public sealed class CommandEntry
    {
        readonly MethodInfo _execute;
        readonly MethodInfo _undo;
        string[] _paramDescriptions;

        public CommandEntry(
            InvokeDescriptor descriptor,
            Type commandType,
            Type paramType,
            Type contextType,
            MethodInfo execute,
            MethodInfo undo)
        {
            Descriptor = descriptor;
            CommandType = commandType;
            ParamType = paramType;
            ContextType = contextType;
            _execute = execute;
            _undo = undo;
        }

        public InvokeDescriptor Descriptor { get; }
        public Type CommandType { get; }

        /// <summary>Null for a command without parameters.</summary>
        public Type ParamType { get; }

        /// <summary><see cref="CommandContext"/> or the subclass the command's Execute takes.</summary>
        public Type ContextType { get; }

        public string Name => Descriptor.Name;
        public CommandHostScope Scope => Descriptor.Scope;
        public string Description => Descriptor.Description ?? "";
        public string[] Aliases => Descriptor.Aliases ?? Array.Empty<string>();
        public int DefaultTimeoutMs => Descriptor.DefaultTimeoutMs;

        /// <summary>Policy class full name for a deferred command; null for a synchronous one.</summary>
        public string Completion => Descriptor is DeferredInvokeDescriptor deferred ? deferred.Completion : null;

        public string[] ParamDescriptions => _paramDescriptions ??= UcpParamBinder.Describe(ParamType);

        /// <summary>The command declares an Undo.</summary>
        public bool CanUndo => _undo != null;

        /// <summary>The parameters name a scene or prefab, which the execution host opens.</summary>
        public bool EditsTarget => ParamType != null && typeof(IEditTargetParams).IsAssignableFrom(ParamType);

        public bool RunsOn(string host) =>
            Scope == CommandHostScope.Any
            || host == CommandRegistry.EditorHost && Scope == CommandHostScope.Editor
            || host == CommandRegistry.RuntimeHost && Scope == CommandHostScope.Runtime;

        public CommandContext CreateContext() => (CommandContext)Activator.CreateInstance(ContextType);

        public object Execute(object parameters, CommandContext ctx) => Call(_execute, parameters, ctx);

        public void Undo(object parameters, CommandContext ctx)
        {
            if (_undo != null)
                Call(_undo, parameters, ctx);
        }

        object Call(MethodInfo method, object parameters, CommandContext ctx)
        {
            var args = ParamType == null ? new object[] { ctx } : new[] { parameters, ctx };
            try
            {
                return method.Invoke(null, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }

    /// <summary>
    /// Every <see cref="UcpCommandAttribute"/> class in the loaded assemblies, found once per domain.
    /// Lookups are per host: the Editor and the runtime agent each see only the commands that run
    /// on them, so the two <c>help</c> commands never shadow each other and the runtime port cannot
    /// reach an Editor-only command.
    /// </summary>
    public static class CommandRegistry
    {
        public const string EditorHost = "editor";
        public const string RuntimeHost = "runtime";

        static readonly object Gate = new();
        static IReadOnlyList<CommandEntry> _entries;
        static List<string> _loadErrors;
        static readonly Dictionary<string, Dictionary<string, CommandEntry>> ByHost = new();

        public static IReadOnlyList<CommandEntry> Entries
        {
            get
            {
                EnsureLoaded();
                return _entries;
            }
        }

        /// <summary>
        /// Command classes that were skipped, each with why. A command's signature is only checked
        /// here, so <c>help</c> reports these and the tests require none.
        /// </summary>
        public static IReadOnlyList<string> LoadErrors
        {
            get
            {
                EnsureLoaded();
                return _loadErrors;
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
                var errors = new List<string>();
                foreach (var type in LoadableTypes())
                {
                    if (!type.IsClass || type.GetCustomAttribute<UcpCommandAttribute>(false) == null)
                        continue;

                    var (entry, error) = Load(type);
                    if (entry != null)
                    {
                        list.Add(entry);
                    }
                    else
                    {
                        var message = $"{type.FullName}: {error}";
                        errors.Add(message);
                        UcpLog.LogError("[ucp-agent] command skipped. " + message);
                    }
                }

                _loadErrors = errors;
                _entries = list
                    .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(e => e.CommandType.FullName, StringComparer.Ordinal)
                    .ToList();
            }
        }

        const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly;

        /// <summary>Checks a command class against the shape <see cref="UcpCommandAttribute"/> describes.</summary>
        public static (CommandEntry entry, string error) Load(Type type)
        {
            if (!(type.IsAbstract && type.IsSealed))
                return (null, "a command is a static class.");

            var mutable = type.GetFields(StaticMembers)
                .FirstOrDefault(f => !f.IsInitOnly && !f.IsLiteral && !f.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false));
            if (mutable != null)
                return (null, $"static field '{mutable.Name}' is not readonly. A command keeps what one run needs on its context.");

            var settable = type.GetProperties(StaticMembers).FirstOrDefault(p => p.SetMethod != null);
            if (settable != null)
                return (null, $"static property '{settable.Name}' has a setter. A command keeps what one run needs on its context.");

            InvokeDescriptor descriptor;
            try
            {
                descriptor = type.GetField("Descriptor", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as InvokeDescriptor
                    ?? type.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as InvokeDescriptor;
            }
            catch (Exception ex)
            {
                return (null, $"its static members throw: {Unwrap(ex).Message}");
            }

            if (descriptor == null || string.IsNullOrEmpty(descriptor.Name))
                return (null, "no public static InvokeDescriptor 'Descriptor' with a name.");

            var executes = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "Execute").ToList();
            if (executes.Count != 1)
                return (null, $"needs exactly one public static Execute, found {executes.Count}.");

            var execute = executes[0];
            if (execute.ReturnType != typeof(object))
                return (null, "Execute returns object: the result, or null.");

            var parameters = execute.GetParameters();
            if (parameters.Length is < 1 or > 2)
                return (null, "Execute takes (TParams p, TContext ctx), or (TContext ctx) for a command without parameters.");

            var contextType = parameters[parameters.Length - 1].ParameterType;
            var paramType = parameters.Length == 2 ? parameters[0].ParameterType : null;
            if (!typeof(CommandContext).IsAssignableFrom(contextType))
                return (null, $"Execute's last parameter is a CommandContext, not {contextType.Name}.");
            if (contextType.IsAbstract || contextType.GetConstructor(Type.EmptyTypes) == null)
                return (null, $"{contextType.Name} needs a public constructor without parameters.");
            if (descriptor.ParamType != null && paramType != descriptor.ParamType)
                return (null, $"the descriptor declares {descriptor.ParamType.Name}, Execute takes {paramType?.Name ?? "no parameters"}.");

            var editsTarget = paramType != null && typeof(IEditTargetParams).IsAssignableFrom(paramType);
            if (editsTarget && !typeof(IEditTargetContext).IsAssignableFrom(contextType))
                return (null, $"its parameters name an edit target, so Execute takes an EditTargetContext, not {contextType.Name}.");
            if (editsTarget && descriptor is DeferredInvokeDescriptor)
                return (null, "a command that edits a target finishes in Execute; it cannot be deferred.");

            var shapeError = ContextShape.Check(contextType);
            if (shapeError != null)
                return (null, shapeError);

            MethodInfo undo = null;
            var undos = type.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "Undo").ToList();
            if (undos.Count > 1)
                return (null, $"has {undos.Count} Undo methods; one at most.");
            if (undos.Count == 1)
            {
                undo = undos[0];
                var undoParams = undo.GetParameters().Select(p => p.ParameterType).ToArray();
                if (undo.ReturnType != typeof(void) || !undoParams.SequenceEqual(parameters.Select(p => p.ParameterType)))
                    return (null, "Undo returns void and takes the same parameters as Execute.");
            }

            return (new CommandEntry(descriptor, type, paramType, contextType, execute, undo), null);
        }

        static IEnumerable<Type> LoadableTypes()
        {
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
                    yield return type;
            }
        }

        public static Exception Unwrap(Exception ex) =>
            ex is TargetInvocationException { InnerException: not null } tie ? tie.InnerException : ex;
    }

    /// <summary>
    /// What a context subclass may hold. A group writes contexts to disk to undo its steps after a
    /// domain reload, so every field is a JSON value or marked <see cref="JsonIgnoreAttribute"/>.
    /// </summary>
    public static class ContextShape
    {
        /// <summary>Null when every member <paramref name="contextType"/> adds serializes; else the first that does not.</summary>
        public static string Check(Type contextType)
        {
            for (var type = contextType; type != null && type != typeof(CommandContext); type = type.BaseType)
            {
                const BindingFlags members = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                foreach (var field in type.GetFields(members))
                {
                    if (!field.IsDefined(typeof(JsonIgnoreAttribute), true) && !IsJsonValue(field.FieldType, 0))
                        return $"{type.Name}.{field.Name} ({field.FieldType.Name}) does not serialize as JSON; mark it [JsonIgnore] or store a path.";
                }

                foreach (var property in type.GetProperties(members))
                {
                    if (property.GetMethod == null || property.SetMethod == null)
                        continue;
                    if (!property.IsDefined(typeof(JsonIgnoreAttribute), true) && !IsJsonValue(property.PropertyType, 0))
                        return $"{type.Name}.{property.Name} ({property.PropertyType.Name}) does not serialize as JSON; mark it [JsonIgnore] or store a path.";
                }
            }

            return null;
        }

        static bool IsJsonValue(Type type, int depth)
        {
            if (depth > 4)
                return false;

            type = Nullable.GetUnderlyingType(type) ?? type;
            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
                return true;
            if (type.IsArray)
                return IsJsonValue(type.GetElementType(), depth + 1);
            if (type.IsGenericType)
            {
                var definition = type.GetGenericTypeDefinition();
                var args = type.GetGenericArguments();
                if (definition == typeof(List<>))
                    return IsJsonValue(args[0], depth + 1);
                if (definition == typeof(Dictionary<,>))
                    return args[0] == typeof(string) && IsJsonValue(args[1], depth + 1);
                return false;
            }

            if (type.Namespace?.StartsWith("UnityEngine", StringComparison.Ordinal) == true
                || type.Namespace?.StartsWith("UnityEditor", StringComparison.Ordinal) == true
                || typeof(Delegate).IsAssignableFrom(type)
                || typeof(IEnumerable).IsAssignableFrom(type)
                || type == typeof(object))
            {
                return false;
            }

            // A plain class of JSON values.
            return type.IsClass && type.GetConstructor(Type.EmptyTypes) != null
                && type.GetFields(BindingFlags.Public | BindingFlags.Instance).All(f => f.IsDefined(typeof(JsonIgnoreAttribute), true) || IsJsonValue(f.FieldType, depth + 1))
                && type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetMethod != null && p.SetMethod != null)
                    .All(p => p.IsDefined(typeof(JsonIgnoreAttribute), true) || IsJsonValue(p.PropertyType, depth + 1));
        }
    }
}
