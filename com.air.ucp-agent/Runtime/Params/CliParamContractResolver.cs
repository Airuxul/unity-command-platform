// @tag cli-param
using System;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Maps <see cref="CliParamAttribute"/> to JSON property names for Newtonsoft.Json.
    /// </summary>
    internal sealed class CliParamContractResolver : DefaultContractResolver
    {
        public static readonly CliParamContractResolver Instance = new();

        public static string GetPropertyKey(CliParamAttribute attr, PropertyInfo property) =>
            string.IsNullOrEmpty(attr?.Key) ? ToCamelCase(property.Name) : attr.Key;

        public static string ToCamelCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            if (name.Length == 1)
                return name.ToLowerInvariant();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            if (member is PropertyInfo propInfo)
            {
                var attr = propInfo.GetCustomAttribute<CliParamAttribute>();
                if (attr != null)
                {
                    property.PropertyName = GetPropertyKey(attr, propInfo);
                    property.Required = attr.Required ? Required.Always : Required.Default;
                }
            }

            return property;
        }
    }
}
