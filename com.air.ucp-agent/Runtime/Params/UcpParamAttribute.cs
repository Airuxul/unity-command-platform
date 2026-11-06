// @tag cli-param
using System;

namespace Air.UcpAgent.Params
{
    /// <summary>
    /// Marks a property on a command parameter class; serialized via Newtonsoft.Json (<see cref="UcpParamBinder"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class UcpParamAttribute : Attribute
    {
        /// <summary>
        /// CLI flag name without leading "--". When null or empty, the property name
        /// with its first letter lowercased is used (e.g. <c>ToolName</c> 鈫?<c>toolName</c>).
        /// </summary>
        public string Key { get; }

        /// <summary>Human-readable description shown in help.</summary>
        public string Description { get; set; } = "";

        /// <summary>When true the command fails with an error if this param is absent.</summary>
        public bool Required { get; set; }

        /// <summary>
        /// Pipe-separated list of accepted values, e.g. "enable|disable|status". Shown in help and
        /// enforced by <see cref="UcpParamBinder"/> (case-insensitive; a comma-separated value is
        /// checked item by item). Leave empty for open-ended strings.
        /// </summary>
        public string AllowedValues { get; set; } = "";

        public UcpParamAttribute() { }

        public UcpParamAttribute(string key)
        {
            Key = key;
        }
    }
}
