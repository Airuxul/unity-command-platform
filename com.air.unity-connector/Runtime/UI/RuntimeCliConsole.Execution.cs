using Air.UnityConnector.Job;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Host;
using Air.UnityGameCore.Runtime.Serialization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Air.UnityConnector.Cli;

namespace Air.UnityConnector
{
    public sealed partial class RuntimeCliConsole
    {
        private void ExecuteLine(string line)
        {
            if (!TryParseLine(line, out var command, out var parameters, out var parseError))
            {
                AppendHistory(FormatHistoryLine("error", parseError));
                return;
            }

            var requestId = Guid.NewGuid().ToString("N");
            var hostName = ResolveHostName();
            var InvokeJobRecord = RuntimeJobStateManager.Create(hostName, command, InvokeCompletionCatalog.GetCompletionKind(command), requestId);
            var context = new InvokeContext
            {
                CommandId = InvokeJobRecord.Id,
                RequestId = requestId,
                Command = command,
                HostName = hostName,
                Notifier = this,
            };

            _pendingCommandNames[InvokeJobRecord.Id] = command;
            AppendHistory(FormatHistoryLine("input", line));

            try
            {
                InvokeExecutor.Execute(context, parameters);
                if (!context.IsCompleted)
                {
                    AppendHistory(FormatHistoryLine("pending", $"{command} ({InvokeJobRecord.Id})"));
                }
            }
            catch (Exception ex)
            {
                AppendHistory(FormatHistoryLine("error", $"{command}: {ex.Message}"));
            }

            RefreshSuggestions(_inputField != null ? _inputField.text : "");
        }

        private static bool TryParseLine(
            string line,
            out string command,
            out Dictionary<string, object> parameters,
            out string error)
        {
            command = null;
            parameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            error = null;

            var tokens = Tokenize(line);
            if (tokens.Count == 0)
            {
                error = "Empty command.";
                return false;
            }

            command = tokens[0];
            for (var i = 1; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (!token.StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Unsupported token '{token}'. Use '--key value'.";
                    return false;
                }

                var key = token[2..];
                if (string.IsNullOrWhiteSpace(key))
                {
                    error = "Parameter key cannot be empty.";
                    return false;
                }

                object value = true;
                if (i + 1 < tokens.Count && !tokens[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    value = ParseValue(tokens[i + 1]);
                    i++;
                }

                if (parameters.TryGetValue(key, out var existing))
                {
                    if (existing is List<object> list)
                    {
                        list.Add(value);
                    }
                    else
                    {
                        parameters[key] = new List<object> { existing, value };
                    }
                }
                else
                {
                    parameters[key] = value;
                }
            }

            foreach (var key in parameters.Keys.ToList())
            {
                if (parameters[key] is List<object> list)
                    parameters[key] = list.Select(v => v?.ToString() ?? "").ToArray();
            }

            return true;
        }

        private static List<string> Tokenize(string input)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(input))
                return tokens;

            var sb = new StringBuilder();
            var inQuotes = false;
            for (var i = 0; i < input.Length; i++)
            {
                var ch = input[i];
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (!inQuotes && char.IsWhiteSpace(ch))
                {
                    if (sb.Length > 0)
                    {
                        tokens.Add(sb.ToString());
                        sb.Clear();
                    }
                    continue;
                }

                sb.Append(ch);
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens;
        }

        private static object ParseValue(string raw)
        {
            if (bool.TryParse(raw, out var b))
                return b;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                return i;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
                return f;
            return raw;
        }
        private static string ToJsonSafe(object value)
        {
            if (value == null)
                return "null";
            if (value is InvokeResult unified)
            {
                if (!string.IsNullOrWhiteSpace(unified.Message))
                    return unified.Message;
                if (unified.Payload != null)
                {
                    ConnectorSerialization.EnsureRegistered();
                    return JsonSerialization.Serialize(unified.Payload);
                }

                return string.IsNullOrWhiteSpace(unified.Code) ? "ok" : unified.Code;
            }
            if (value is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("message", out var msg) && msg != null)
                    return msg.ToString();
                if (dict.TryGetValue("error", out var err) && err != null)
                    return err.ToString();
                if (dict.TryGetValue("level", out var level) && level != null)
                    return $"level={level}";
            }
            try
            {
                ConnectorSerialization.EnsureRegistered();
                return JsonSerialization.Serialize(value);
            }
            catch
            {
                return value.ToString();
            }
        }
        void IInvokeJobNotifier.MarkRunning(string commandId)
        {
            if (_pendingCommandNames.TryGetValue(commandId, out var command))
                AppendHistory(FormatHistoryLine("running", $"{command} ({commandId})"));
        }

        void IInvokeJobNotifier.Succeed(string commandId, object result)
        {
            if (!_pendingCommandNames.TryGetValue(commandId, out var command))
                command = "unknown";
            AppendHistory(FormatHistoryLine("ok", $"{command}: {ToJsonSafe(result)}"));
            _pendingCommandNames.Remove(commandId);
        }

        void IInvokeJobNotifier.Fail(string commandId, string error)
        {
            if (!_pendingCommandNames.TryGetValue(commandId, out var command))
                command = "unknown";
            AppendHistory(FormatHistoryLine("error", $"{command}: {error}"));
            _pendingCommandNames.Remove(commandId);
        }
    }
}
