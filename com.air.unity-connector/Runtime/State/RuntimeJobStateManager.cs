using Air.UnityConnector.Job;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Air.UnityConnector.Invoke;

namespace Air.UnityConnector
{
    public static class RuntimeJobStateManager
    {
        private const string PrefsPrefix = "Air.UnityConnector.RuntimeCommandStates.";
        private static readonly int MainThreadId = Thread.CurrentThread.ManagedThreadId;
        private static readonly Dictionary<string, HostState> Hosts = new(StringComparer.OrdinalIgnoreCase);

        public static InvokeJobRecord Create(string host, string command, string completionKind, string requestId)
        {
            var state = GetOrCreate(host);
            return state.WithLock(() =>
                JobStateCore.Create(state.Commands, command, completionKind, requestId, JobStateCore.UtcNowMs, state.Save));
        }

        public static InvokeJobRecord Get(string host, string id)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(id)) return null;
            var state = GetOrCreate(host);
            return state.WithLock(() => state.Commands.TryGetValue(id, out var command) ? command : null);
        }

        public static void MarkRunning(string host, string id)
        {
            if (!TryGetMutable(host, id, out var state, out var command)) return;
            state.WithLock(() =>
            {
                if (JobStateCore.ShouldSkip(command)) return;
                command.Status = InvokeJobStatus.Running;
                command.UpdatedAtUtcMs = JobStateCore.UtcNowMs();
                state.Save();
            });
        }

        public static void Succeed(string host, string id, object result)
        {
            if (!TryGetMutable(host, id, out var state, out var command)) return;
            state.WithLock(() =>
                JobStateCore.MarkSucceeded(command, result, JobStateCore.UtcNowMs(), true, state.Save));
        }

        public static void Fail(string host, string id, string error)
        {
            if (!TryGetMutable(host, id, out var state, out var command)) return;
            state.WithLock(() =>
                JobStateCore.MarkFailed(command, error, JobStateCore.UtcNowMs(), state.Save));
        }

        public static void Tick(string host)
        {
            if (string.IsNullOrEmpty(host)) return;
            var state = GetOrCreate(host);
            state.FlushIfDirty();
            if (state.WithLock(() => state.Commands.Count) == 0) return;

            state.WithLock(() =>
                JobStateCore.TickJobs(
                    state.Commands.Values,
                    state.Save,
                    resolveCompletion: _ => null,
                    resolveMissingPolicyError: command => $"No runtime completion policy for '{command.CompletionKind}'.",
                    canRunWithoutPolicy: command =>
                        string.IsNullOrEmpty(command.CompletionKind) || string.Equals(command.CompletionKind, InvokeCompletionCatalog.CompletionDeferred, StringComparison.Ordinal),
                    serializeResultJson: true));
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

        private static bool TryGetMutable(string host, string id, out HostState state, out InvokeJobRecord command)
        {
            state = null;
            command = null;
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(id)) return false;
            state = GetOrCreate(host);
            var hostState = state;
            InvokeJobRecord found = null;
            var ok = hostState.WithLock(() => hostState.Commands.TryGetValue(id, out found));
            if (!ok)
                return false;
            command = found;
            return true;
        }

        private sealed class HostState
        {
            public readonly string Host;
            public readonly Dictionary<string, InvokeJobRecord> Commands = new();
            private readonly object _sync = new();
            private bool _dirty;

            public HostState(string host)
            {
                Host = host;
                Load();
            }

            public T WithLock<T>(Func<T> action)
            {
                lock (_sync)
                {
                    return action();
                }
            }

            public void WithLock(Action action)
            {
                lock (_sync)
                {
                    action();
                }
            }

            public void Save()
            {
                if (Thread.CurrentThread.ManagedThreadId != MainThreadId)
                {
                    _dirty = true;
                    return;
                }

                var wrapper = new InvokeJobListWrapper { Items = Commands.Values.ToList() };
                PlayerPrefs.SetString(PrefsPrefix + Host, JsonUtility.ToJson(wrapper));
                PlayerPrefs.Save();
                _dirty = false;
            }

            public void FlushIfDirty()
            {
                if (!_dirty || Thread.CurrentThread.ManagedThreadId != MainThreadId)
                    return;
                WithLock(Save);
            }

            private void Load()
            {
                var json = PlayerPrefs.GetString(PrefsPrefix + Host, "");
                if (string.IsNullOrEmpty(json)) return;
                try
                {
                    var wrapper = JsonUtility.FromJson<InvokeJobListWrapper>(json);
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
