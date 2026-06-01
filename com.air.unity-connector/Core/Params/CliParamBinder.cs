using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;

namespace UnityCliConnector
{
    /// <summary>
    /// Binds CLI parameter dictionaries to typed param classes via Newtonsoft.Json.
    /// </summary>
    public static class CliParamBinder
    {
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            ContractResolver = CliParamContractResolver.Instance,
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

            var normalized = NormalizeKeys(paramType, values);
            var requiredError = ValidateRequired(paramType, normalized);
            if (requiredError != null)
                return (null, requiredError);

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

        public static string[] Describe(Type paramType)
        {
            if (paramType == null)
                return Array.Empty<string>();

            var lines = new List<string>();
            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<CliParamAttribute>();
                if (attr == null)
                    continue;

                var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                var typeName = type == typeof(string) ? "string"
                    : type == typeof(bool) ? "bool"
                    : type == typeof(int) ? "int"
                    : type == typeof(float) ? "float"
                    : type == typeof(string[]) ? "string[]"
                    : type.Name.ToLowerInvariant();

                var key = CliParamContractResolver.GetPropertyKey(attr, prop);
                var values = string.IsNullOrEmpty(attr.AllowedValues) ? "" : $" [{attr.AllowedValues}]";
                var req = attr.Required ? " (required)" : "";
                lines.Add($"--{key} <{typeName}>{values} {attr.Description}{req}".TrimEnd());
            }

            return lines.ToArray();
        }

        private static Dictionary<string, object> NormalizeKeys(Type paramType, Dictionary<string, object> values)
        {
            var source = values ?? new Dictionary<string, object>();
            var result = new Dictionary<string, object>(StringComparer.Ordinal);

            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<CliParamAttribute>();
                if (attr == null)
                    continue;

                var key = CliParamContractResolver.GetPropertyKey(attr, prop);
                if (TryGetValue(source, key, attr, out var raw))
                    result[key] = raw;
            }

            return result;
        }

        private static string ValidateRequired(Type paramType, Dictionary<string, object> normalized)
        {
            foreach (var prop in paramType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = prop.GetCustomAttribute<CliParamAttribute>();
                if (attr == null || !attr.Required)
                    continue;

                var key = CliParamContractResolver.GetPropertyKey(attr, prop);
                if (!normalized.TryGetValue(key, out var raw) || raw == null)
                    return $"Missing required parameter '--{key}'.";

                if (raw is string s && string.IsNullOrWhiteSpace(s))
                    return $"Missing required parameter '--{key}'.";
            }

            return null;
        }

        private static bool TryGetValue(
            Dictionary<string, object> dict,
            string key,
            CliParamAttribute attr,
            out object raw)
        {
            foreach (var pair in dict)
            {
                if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase) &&
                    pair.Value != null)
                {
                    raw = pair.Value;
                    return true;
                }
            }

            if (!string.IsNullOrWhiteSpace(attr.AlternateKeys))
            {
                foreach (var alt in attr.AlternateKeys.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var altKey = alt.Trim();
                    foreach (var pair in dict)
                    {
                        if (altKey.Length > 0 &&
                            string.Equals(pair.Key, altKey, StringComparison.OrdinalIgnoreCase) &&
                            pair.Value != null)
                        {
                            raw = pair.Value;
                            return true;
                        }
                    }
                }
            }

            raw = null;
            return false;
        }
    }
}
