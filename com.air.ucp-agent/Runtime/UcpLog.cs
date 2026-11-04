using UnityEngine;

namespace Air.UcpAgent
{
    /// <summary>
    /// Gates ucp-agent internal Unity Console output. Default: enabled.
    /// Does not affect CLI log commands or file trace logs.
    /// </summary>
    public static class UcpLog
    {
        /// <summary>When false, <see cref="Log"/>, <see cref="LogWarning"/>, and <see cref="LogError"/> are no-ops.</summary>
        public static bool Enabled { get; set; } = true;

        public static void Log(string message)
        {
            if (Enabled && !string.IsNullOrEmpty(message))
                Debug.Log(message);
        }

        public static void LogWarning(string message)
        {
            if (Enabled && !string.IsNullOrEmpty(message))
                Debug.LogWarning(message);
        }

        public static void LogError(string message)
        {
            if (Enabled && !string.IsNullOrEmpty(message))
                Debug.LogError(message);
        }
    }
}
