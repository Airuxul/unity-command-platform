// @tag cli-param
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Binds CLI parameter dictionaries to typed param classes via Newtonsoft.Json.
    /// </summary>
    public static class UcpParamBinder
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = UcpParamContractResolver.Instance,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public static (T result, string error) Bind<T>(Dictionary<string, object> values) where T : new()
        {
            var (result, error) = Bind(typeof(T), values);
            return ((T)result, error);
        }

        public static (object result, string error) Bind(Type paramType, Dictionary<string, object> values)
        {
            if (paramType == null)
                return (null, "Internal error: parameter type is missing.");

            var (normalized, unknown) = NormalizeKeys(paramType, values);
            if (unknown != null)
                return (null, unknown);

            var invalid = ValidateRequired(paramType, normalized) ?? ValidateAllowed(paramType, normalized);
            if (invalid != null)
                return (null, invalid);

            try
            {
                var json = JsonConvert.SerializeObject(normalized);
                var obj = JsonConvert.DeserializeObject(json, paramType, SerializerSettings);
                if (obj == null && paramType.GetConstructor(Type.EmptyTypes) != null)
                    obj = Activator.CreateInstance(paramType);
                return (obj, null);
            }
            catch (JsonException ex)
            {
                return (null, $"Invalid parameters: {ex.Message}");
            }
        }

        /// <summary>The help lines: <c>--key &lt;type&gt; [allowed] description (required)</c>.</summary>
        public static string[] Describe(Type paramType)
        {
            var lines = new List<string>();
            foreach (var param in Inspect(paramType))
            {
                var values = string.IsNullOrEmpty(param.AllowedValues) ? "" : $" [{param.AllowedValues}]";
                var req = param.Required ? " (required)" : "";
                lines.Add($"--{param.Key} <{param.TypeName}>{values} {param.Description}{req}".TrimEnd());
            }

            return lines.ToArray();
        }

        /// <summary>The parameters <paramref name="paramType"/> declares, in declaration order.</summary>
        public static IReadOnlyList<UcpParamInfo> Inspect(Type paramType)
        {
            if (paramType == null)
                return Array.Empty<UcpParamInfo>();

            var list = new List<UcpParamInfo>();
            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<UcpParamAttribute>();
                if (attr == null)
                    continue;

                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var typeName = type == typeof(string) ? "string"
                    : type == typeof(bool) ? "bool"
                    : type == typeof(int) ? "int"
                    : type == typeof(float) ? "float"
                    : type == typeof(string[]) ? "string[]"
                    : type.Name.ToLowerInvariant();

                list.Add(new UcpParamInfo(
                    UcpParamContractResolver.GetPropertyKey(attr, prop),
                    typeName,
                    attr.Required,
                    attr.AllowedValues,
                    attr.Description));
            }

            return list;
        }

        private static (Dictionary<string, object> normalized, string error) NormalizeKeys(
            Type paramType,
            Dictionary<string, object> values)
        {
            var source = values ?? new Dictionary<string, object>();
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<UcpParamAttribute>();
                if (attr == null)
                    continue;

                var key = UcpParamContractResolver.GetPropertyKey(attr, prop);
                if (TryGetValue(source, key, consumed, out var raw))
                    result[key] = raw;
            }

            foreach (var pair in source)
            {
                if (consumed.Contains(pair.Key))
                    continue;

                var legal = string.Join(", ", Inspect(paramType).Select(p => p.Key));
                return (null, $"Unknown parameter '{pair.Key}'. Valid: {legal}.");
            }

            return (result, null);
        }

        private static string ValidateRequired(Type paramType, Dictionary<string, object> normalized)
        {
            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<UcpParamAttribute>();
                if (attr == null || !attr.Required)
                    continue;

                var key = UcpParamContractResolver.GetPropertyKey(attr, prop);
                if (!normalized.TryGetValue(key, out var raw) || raw == null)
                    return $"Missing required parameter '--{key}'.";

                if (raw is string s && string.IsNullOrWhiteSpace(s))
                    return $"Missing required parameter '--{key}'.";
            }

            return null;
        }

        /// <summary>
        /// A value outside <see cref="UcpParamAttribute.AllowedValues"/> fails here instead of the
        /// command quietly falling back to its default. Comma-separated lists are checked item by item.
        /// </summary>
        private static string ValidateAllowed(Type paramType, Dictionary<string, object> normalized)
        {
            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<UcpParamAttribute>();
                if (attr == null || string.IsNullOrEmpty(attr.AllowedValues))
                    continue;

                var key = UcpParamContractResolver.GetPropertyKey(attr, prop);
                if (!normalized.TryGetValue(key, out var raw) || raw == null)
                    continue;

                if (!(raw is string text))
                    return $"Invalid value for '--{key}'. Expected a string. Valid: {attr.AllowedValues.Replace("|", ", ")}.";

                var allowed = attr.AllowedValues.Split('|');
                var canonical = new List<string>();
                foreach (var item in text.Split(','))
                {
                    var value = item.Trim();
                    if (value.Length == 0)
                        continue;
                    var match = Array.Find(allowed, a => string.Equals(a.Trim(), value, StringComparison.OrdinalIgnoreCase));
                    if (match == null)
                        return $"Invalid value '{value}' for '--{key}'. Valid: {attr.AllowedValues.Replace("|", ", ")}.";
                    canonical.Add(match.Trim());
                }

                normalized[key] = string.Join(",", canonical);
            }

            return null;
        }

        private static bool TryGetValue(
            Dictionary<string, object> dict,
            string key,
            HashSet<string> consumed,
            out object raw)
        {
            foreach (var pair in dict)
            {
                if (!string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
                    continue;

                consumed.Add(pair.Key);
                if (pair.Value == null)
                    continue;

                raw = pair.Value;
                return true;
            }

            raw = null;
            return false;
        }
    }

    public sealed class UcpParamInfo
    {
        public UcpParamInfo(string key, string typeName, bool required, string allowedValues, string description)
        {
            Key = key;
            TypeName = typeName;
            Required = required;
            AllowedValues = allowedValues ?? "";
            Description = description ?? "";
        }

        public string Key { get; }

        /// <summary>string, bool, int, float, string[], or the lowercased CLR name (jtoken, list`1, ...).</summary>
        public string TypeName { get; }

        public bool Required { get; }
        public string AllowedValues { get; }
        public string Description { get; }
    }
}
