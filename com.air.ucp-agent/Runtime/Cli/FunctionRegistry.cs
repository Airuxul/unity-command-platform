using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Air.UcpAgent.Invoke;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// Marks a public static method that <c>call</c> runs by <see cref="Name"/>. Its parameters are
    /// bound from <c>args</c> by parameter name; one of type <see cref="CommandContext"/> receives the
    /// run's context. The return value is the result.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public sealed class UcpFunctionAttribute : Attribute
    {
        public UcpFunctionAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public string Description { get; set; } = "";

        /// <summary>
        /// Which host lists the function. In Play Mode the runtime agent runs inside the Editor and
        /// sees Editor assemblies too, so an Editor-only function says so here.
        /// </summary>
        public CommandHostScope Scope { get; set; } = CommandHostScope.Any;
    }

    public sealed class FunctionEntry
    {
        readonly ParameterInfo[] _parameters;

        public FunctionEntry(UcpFunctionAttribute attribute, MethodInfo method)
        {
            Name = attribute.Name.Trim();
            Description = attribute.Description ?? "";
            Scope = attribute.Scope;
            Method = method;
            _parameters = method.GetParameters();
        }

        public string Name { get; }
        public string Description { get; }
        public CommandHostScope Scope { get; }
        public MethodInfo Method { get; }

        public bool RunsOn(string host) =>
            Scope == CommandHostScope.Any
            || host == CommandRegistry.EditorHost && Scope == CommandHostScope.Editor
            || host == CommandRegistry.RuntimeHost && Scope == CommandHostScope.Runtime;

        /// <summary>The parameters a caller passes, as <c>name &lt;type&gt;</c>, with <c>(optional)</c> for those with a default.</summary>
        public string[] ParamDescriptions =>
            _parameters
                .Where(p => !IsContext(p))
                .Select(p => $"{p.Name} <{TypeName(p.ParameterType)}>{(p.HasDefaultValue ? " (optional)" : "")}")
                .ToArray();

        public object Invoke(Dictionary<string, object> args, CommandContext ctx)
        {
            var source = args ?? new Dictionary<string, object>();
            var values = new object[_parameters.Length];
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < _parameters.Length; i++)
            {
                var parameter = _parameters[i];
                if (IsContext(parameter))
                {
                    values[i] = ctx;
                    continue;
                }

                var key = source.Keys.FirstOrDefault(k => string.Equals(k, parameter.Name, StringComparison.OrdinalIgnoreCase));
                if (key == null)
                {
                    if (!parameter.HasDefaultValue)
                        throw new ArgumentException($"'{Name}' needs '{parameter.Name}' ({TypeName(parameter.ParameterType)}).");

                    values[i] = parameter.DefaultValue;
                    continue;
                }

                used.Add(key);
                values[i] = Convert(source[key], parameter);
            }

            var unknown = source.Keys.FirstOrDefault(k => !used.Contains(k));
            if (unknown != null)
            {
                var valid = string.Join(", ", _parameters.Where(p => !IsContext(p)).Select(p => p.Name));
                throw new ArgumentException($"'{Name}' has no parameter '{unknown}'. Valid: {(valid.Length > 0 ? valid : "none")}.");
            }

            try
            {
                return Method.Invoke(null, values);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }

        object Convert(object value, ParameterInfo parameter)
        {
            var type = parameter.ParameterType;
            if (value == null)
            {
                if (!type.IsValueType || Nullable.GetUnderlyingType(type) != null)
                    return null;
                throw new ArgumentException($"'{Name}': '{parameter.Name}' is {TypeName(type)} and cannot be null.");
            }

            try
            {
                var token = value as JToken ?? JToken.FromObject(value);
                return token.ToObject(type);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidCastException or ArgumentException or OverflowException)
            {
                throw new ArgumentException($"'{Name}': '{parameter.Name}' expects {TypeName(type)}, got {JsonConvert.SerializeObject(value)}.");
            }
        }

        static bool IsContext(ParameterInfo parameter) => parameter.ParameterType == typeof(CommandContext);

        static string TypeName(Type type)
        {
            type = Nullable.GetUnderlyingType(type) ?? type;
            return type == typeof(string) ? "string"
                : type == typeof(bool) ? "bool"
                : type == typeof(int) ? "int"
                : type == typeof(long) ? "long"
                : type == typeof(float) ? "float"
                : type == typeof(double) ? "double"
                : type.IsArray ? TypeName(type.GetElementType()) + "[]"
                : type.Name.ToLowerInvariant();
        }
    }

    /// <summary>Every <see cref="UcpFunctionAttribute"/> method in the loaded assemblies, found once per domain.</summary>
    public static class FunctionRegistry
    {
        static readonly object Gate = new();
        static IReadOnlyList<FunctionEntry> _entries;
        static List<string> _loadErrors;

        public static IReadOnlyList<FunctionEntry> Entries
        {
            get
            {
                EnsureLoaded();
                return _entries;
            }
        }

        /// <summary>Functions that were skipped, each with why.</summary>
        public static IReadOnlyList<string> LoadErrors
        {
            get
            {
                EnsureLoaded();
                return _loadErrors;
            }
        }

        public static IEnumerable<FunctionEntry> ForHost(string host) => Entries.Where(e => e.RunsOn(host));

        public static FunctionEntry Find(string host, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return null;

            return ForHost(host).FirstOrDefault(e => string.Equals(e.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        static void EnsureLoaded()
        {
            if (_entries != null)
                return;

            lock (Gate)
            {
                if (_entries != null)
                    return;

                var list = new List<FunctionEntry>();
                var errors = new List<string>();
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
                        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
                        foreach (var method in type.GetMethods(all))
                        {
                            var attribute = method.GetCustomAttribute<UcpFunctionAttribute>(false);
                            if (attribute == null)
                                continue;

                            var where = $"{type.FullName}.{method.Name}";
                            var error = Check(attribute, method);
                            if (error != null)
                            {
                                errors.Add($"{where}: {error}");
                                continue;
                            }

                            list.Add(new FunctionEntry(attribute, method));
                        }
                    }
                }

                // Two functions with one name: neither runs, so a call never picks one by accident.
                foreach (var duplicate in list.GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList())
                {
                    var owners = string.Join(", ", duplicate.Select(e => $"{e.Method.DeclaringType?.FullName}.{e.Method.Name}"));
                    errors.Add($"function '{duplicate.Key}' is declared by {owners}; none of them is listed.");
                    list.RemoveAll(e => string.Equals(e.Name, duplicate.Key, StringComparison.OrdinalIgnoreCase));
                }

                foreach (var error in errors)
                    UcpLog.LogError("[ucp-agent] function skipped. " + error);

                _loadErrors = errors;
                _entries = list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }

        static string Check(UcpFunctionAttribute attribute, MethodInfo method)
        {
            if (string.IsNullOrWhiteSpace(attribute.Name))
                return "[UcpFunction] needs a name.";
            if (!method.IsStatic || !method.IsPublic)
                return "a function is a public static method.";
            if (method.ContainsGenericParameters)
                return "a function cannot be generic.";
            if (method.GetParameters().Any(p => p.ParameterType.IsByRef))
                return "a function cannot take ref or out parameters.";
            if (method.GetParameters().Any(p => p.ParameterType != typeof(CommandContext) && typeof(CommandContext).IsAssignableFrom(p.ParameterType)))
                return "a function receives the plain CommandContext; a subclass is for commands.";
            if (method.GetParameters().Count(p => p.ParameterType == typeof(CommandContext)) > 1)
                return "a function takes at most one CommandContext.";

            return null;
        }
    }
}
