using System;

namespace UnityCliConnector
{
    /// <summary>
    /// Marks a property on a command parameter class; serialized via Newtonsoft.Json (<see cref="CliParamBinder"/>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class CliParamAttribute : Attribute
    {
        /// <summary>
        /// CLI flag name without leading "--". When null or empty, the property name
        /// with its first letter lowercased is used (e.g. <c>ToolName</c> → <c>toolName</c>).
        /// </summary>
        public string Key { get; }

        /// <summary>Human-readable description shown in help.</summary>
        public string Description { get; set; } = "";

        /// <summary>When true the command fails with an error if this param is absent.</summary>
        public bool Required { get; set; }

        /// <summary>
        /// Pipe-separated list of accepted values shown in help, e.g. "enable|disable|status".
        /// Does NOT enforce validation at runtime (too restrictive for open-ended strings).
        /// </summary>
        public string AllowedValues { get; set; } = "";

        /// <summary>Comma-separated alternate JSON keys for the same property.</summary>
        public string AlternateKeys { get; set; } = "";

        public CliParamAttribute() { }

        public CliParamAttribute(string key)
        {
            Key = key;
        }
    }
}
