using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnityCliConnector
{
    public static class RuntimeCommandStateManager
    {
        private const string PrefsPrefix = "UnityCliConnector.RuntimeCommandStates.";
        private static readonly Dictionary<string, HostState> Hosts = new(StringComparer.OrdinalIgnoreCase);

        public static CommandRecord Create(string host, string command, string completionKind, string requestId)
        {
            var state = GetOrCreate(host);
            return CommandStateCore.Create(state.Commands, command, completionKind, requestId, CommandStateCore.UtcNowMs, state.Save);
        }

        public static CommandRecord Get(string host, string id)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(id)) return null;
            return GetOrCreate(host).Commands.TryGetValue(id, out var command) ? command : null;
        }

        public static void MarkRunning(string host, string id)
        {
            if (!TryGetMutable(host, id, out var state, out var command) || CommandStateCore.ShouldSkip(command)) return;
            command.Status = CommandStatus.Running;
            command.UpdatedAtUtcMs = CommandStateCore.UtcNowMs();
            state.Save();
        }

        public static void Succeed(string host, string id, object result)
        {
            if (!TryGetMutable(host, id, out var state, out var command)) return;
            CommandStateCore.MarkSucceeded(command, result, CommandStateCore.UtcNowMs(), true, state.Save);
        }

        public static void Fail(string host, string id, string error)
        {
            if (!TryGetMutable(host, id, out var state, out var command)) return;
            CommandStateCore.MarkFailed(command, error, CommandStateCore.UtcNowMs(), state.Save);
        }

        public static void Tick(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            var state = GetOrCreate(host);
            if (state.Commands.Count == 0) return;

            CommandStateCore.TickCommands(
                state.Commands.Values,
                state.Save,
                resolveCompletion: _ => null,
                resolveMissingPolicyError: command => $"No runtime completion policy for '{command.CompletionKind}'.",
                canRunWithoutPolicy: command =>
                    string.IsNullOrEmpty(command.CompletionKind) || string.Equals(command.CompletionKind, CommandCompletionCatalog.CompletionDeferred, StringComparison.Ordinal),
                serializeResultJson: true);
        }

        private static HostState GetOrCreate(string host)
        {
            if (!Hosts.TryGetValue(host, out var state))
            {
                state = new HostState(host);
                Hosts[host] = state;
            }
            return state;
        }

        private static bool TryGetMutable(string host, string id, out HostState state, out CommandRecord command)
        {
            state = null;
            command = null;
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(id)) return false;
            state = GetOrCreate(host);
            return state.Commands.TryGetValue(id, out command);
        }

        private sealed class HostState
        {
            public readonly string Host;
            public readonly Dictionary<string, CommandRecord> Commands = new();

            public HostState(string host)
            {
                Host = host;
                Load();
            }

            public void Save()
            {
                var wrapper = new CommandListWrapper { Items = Commands.Values.ToList() };
                PlayerPrefs.SetString(PrefsPrefix + Host, JsonUtility.ToJson(wrapper));
                PlayerPrefs.Save();
            }

            private void Load()
            {
                var json = PlayerPrefs.GetString(PrefsPrefix + Host, "");
                if (string.IsNullOrEmpty(json)) return;
                try
                {
                    var wrapper = JsonUtility.FromJson<CommandListWrapper>(json);
                    if (wrapper?.Items == null) return;
                    foreach (var item in wrapper.Items)
                    {
                        if (!string.IsNullOrEmpty(item?.Id)) Commands[item.Id] = item;
                    }
                }
                catch
                {
                    Commands.Clear();
                }
            }
        }
    }
}
