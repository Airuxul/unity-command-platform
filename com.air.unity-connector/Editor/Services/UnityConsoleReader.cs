using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace UnityCliConnector.Editor.Services
{
    /// <summary>Reads Unity Editor console via LogEntries reflection (Unity 2021.3+).</summary>
    public static class UnityConsoleReader
    {
        private static MethodInfo _startGettingEntries;
        private static MethodInfo _endGettingEntries;
        private static MethodInfo _clear;
        private static MethodInfo _getCount;
        private static MethodInfo _getEntry;
        private static FieldInfo _modeField;
        private static FieldInfo _messageField;
        private static FieldInfo _fileField;
        private static FieldInfo _lineField;
        private static Type _logEntryType;
        private static string _initError;

        static UnityConsoleReader()
        {
            try
            {
                var editorAssembly = typeof(UnityEditor.EditorApplication).Assembly;
                var logEntriesType = editorAssembly.GetType("UnityEditor.LogEntries")
                    ?? throw new InvalidOperationException("UnityEditor.LogEntries not found");

                const BindingFlags sf = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                const BindingFlags inf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _startGettingEntries = RequireMethod(logEntriesType, "StartGettingEntries", sf);
                _endGettingEntries = RequireMethod(logEntriesType, "EndGettingEntries", sf);
                _clear = RequireMethod(logEntriesType, "Clear", sf);
                _getCount = RequireMethod(logEntriesType, "GetCount", sf);
                _getEntry = RequireMethod(logEntriesType, "GetEntryInternal", sf);

                _logEntryType = editorAssembly.GetType("UnityEditor.LogEntry")
                    ?? throw new InvalidOperationException("UnityEditor.LogEntry not found");
                _modeField = RequireField(_logEntryType, "mode", inf);
                _messageField = RequireField(_logEntryType, "message", inf);
                _fileField = _logEntryType.GetField("file", inf);
                _lineField = _logEntryType.GetField("line", inf);
            }
            catch (Exception ex)
            {
                _initError = ex.Message;
            }
        }

        public static bool IsReady => _initError == null && _startGettingEntries != null;

        public static string InitError => _initError;

        public static void Clear()
        {
            EnsureReady();
            _clear.Invoke(null, null);
        }

        public static List<Dictionary<string, object>> Read(ConsoleReadOptions options)
        {
            EnsureReady();
            var types = ParseTypes(options.TypeFilter);
            var stacktrace = (options.Stacktrace ?? "user").ToLowerInvariant();
            var max = options.MaxEntries;
            var entries = new List<Dictionary<string, object>>();

            try
            {
                _startGettingEntries.Invoke(null, null);
                var total = (int)_getCount.Invoke(null, null);
                var logEntry = Activator.CreateInstance(_logEntryType);

                for (var i = 0; i < total; i++)
                {
                    _getEntry.Invoke(null, new[] { i, logEntry });
                    var mode = (int)_modeField.GetValue(logEntry);
                    var message = (string)_messageField.GetValue(logEntry);
                    if (string.IsNullOrEmpty(message))
                        continue;

                    var logType = GetLogTypeFromMode(mode);
                    if (!WantsType(types, logType))
                        continue;

                    var item = new Dictionary<string, object>
                    {
                        ["type"] = LogTypeToString(logType),
                        ["message"] = FormatMessage(message, stacktrace),
                    };

                    if (_fileField != null)
                    {
                        var file = _fileField.GetValue(logEntry) as string;
                        if (!string.IsNullOrEmpty(file))
                            item["file"] = file;
                    }

                    if (_lineField != null)
                    {
                        var line = _lineField.GetValue(logEntry);
                        if (line is int lineNo && lineNo > 0)
                            item["line"] = lineNo;
                    }

                    entries.Add(item);
                    if (max.HasValue && entries.Count >= max.Value)
                        break;
                }
            }
            finally
            {
                try
                {
                    _endGettingEntries.Invoke(null, null);
                }
                catch
                {
                    // ignored
                }
            }

            return entries;
        }

        private static void EnsureReady()
        {
            if (!IsReady)
                throw new InvalidOperationException(
                    $"Unity console reader failed to initialize: {_initError ?? "unknown"}");
        }

        private static MethodInfo RequireMethod(Type type, string name, BindingFlags flags) =>
            type.GetMethod(name, flags) ?? throw new InvalidOperationException($"Method not found: {name}");

        private static FieldInfo RequireField(Type type, string name, BindingFlags flags) =>
            type.GetField(name, flags) ?? throw new InvalidOperationException($"Field not found: {name}");

        private static HashSet<string> ParseTypes(string raw)
        {
            var text = string.IsNullOrWhiteSpace(raw) ? "error,warning,log" : raw;
            return new HashSet<string>(
                text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLowerInvariant())
                    .Where(t => t.Length > 0));
        }

        private static bool WantsType(HashSet<string> types, LogType logType)
        {
            if (logType == LogType.Exception || logType == LogType.Assert)
                return types.Contains("error");
            return types.Contains(LogTypeToString(logType));
        }

        private static string LogTypeToString(LogType logType)
        {
            switch (logType)
            {
                case LogType.Error:
                case LogType.Exception:
                case LogType.Assert:
                    return "error";
                case LogType.Warning:
                    return "warning";
                default:
                    return "log";
            }
        }

        private static string FormatMessage(string message, string mode)
        {
            switch (mode)
            {
                case "full":
                    return message;
                case "user":
                    var lines = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    var sb = new StringBuilder();
                    foreach (var line in lines)
                    {
                        if (line.Contains("UnityEngine.Debug:") ||
                            line.Contains("UnityEditor.EditorGUIUtility:") ||
                            line.Contains("Unity.Entities.SystemState:") ||
                            line.Contains("(at Library/") ||
                            line.Contains("(at ./Library/"))
                            continue;
                        if (sb.Length > 0)
                            sb.Append('\n');
                        sb.Append(line);
                    }

                    return sb.ToString();
                default:
                    var first = message.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                    return first.Length > 0 ? first[0] : message;
            }
        }

        private const int ErrorMask =
            (1 << 0) | (1 << 6) | (1 << 8) | (1 << 11) | (1 << 13);

        private const int WarningMask =
            (1 << 7) | (1 << 9) | (1 << 12);

        private const int ExceptionMask =
            (1 << 1) | (1 << 4) | (1 << 17) | (1 << 21);

        private static LogType GetLogTypeFromMode(int mode)
        {
            if ((mode & ExceptionMask) != 0)
                return LogType.Exception;
            if ((mode & ErrorMask) != 0)
                return LogType.Error;
            if ((mode & WarningMask) != 0)
                return LogType.Warning;
            return LogType.Log;
        }

        public sealed class ConsoleReadOptions
        {
            public string TypeFilter { get; set; }
            public int? MaxEntries { get; set; }
            public string Stacktrace { get; set; }
        }
    }
}
