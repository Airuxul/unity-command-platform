using System;
using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent
{
    /// <summary>
    /// Jobs of deferred editor commands, kept in memory, SessionState and <see cref="EditorJobLedger"/>
    /// so they survive the domain reload a compile or Play Mode entry causes. A job lives from the
    /// moment its command defers until <see cref="Editor.EditorAgentBootstrap"/> has written its result.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorJobStateManager
    {
        const string SessionKey = "Air.UcpAgent.Jobs";

        /// <summary>Records from before 0.5 carry no deadline; they keep the old no-progress rule.</summary>
        const int LegacyNoProgressTimeoutMs = 20000;

        /// <summary>
        /// A finished job older than this has no caller left (ucp-host drops results after 10 min).
        /// Versions before 0.5 never removed jobs, so the first reload after an upgrade finds them all.
        /// </summary>
        const long FinishedRetentionMs = 10 * 60_000;

        const string TimeoutError = "Command timed out before it finished.";
        const string LegacyTimeoutError = "Command timed out without progress (20s).";
        const string ReloadOrphanError =
            "Command lost after domain reload (no completion policy to resume).";

        static readonly Dictionary<string, ICompletionPolicy> Policies = new()
        {
            { InvokeCompletionCatalog.CompletionCompilation, new Completion.CompilationPolicy() },
            { InvokeCompletionCatalog.CompletionEnterPlay, new Completion.EnterPlayModePolicy() },
            { InvokeCompletionCatalog.CompletionExitPlay, new Completion.ExitPlayModePolicy() },
            { InvokeCompletionCatalog.CompletionUiWait, new Completion.UiWaitPolicy() },
            { InvokeCompletionCatalog.CompletionTests, new Completion.TestRunPolicy() },
            { InvokeCompletionCatalog.CompletionPackages, new Completion.PackagePolicy() },
        };

        static Dictionary<string, InvokeJobRecord> _commands = new();

        /// <summary>The <see cref="IDeferredJobs"/> the command executor uses on the Editor host.</summary>
        public static readonly IDeferredJobs Jobs = new JobStore();

        static EditorJobStateManager()
        {
            Reload();
            EditorApplication.update += Tick;
        }

        public static IReadOnlyDictionary<string, InvokeJobRecord> AllCommands => _commands;

        public static InvokeJobRecord Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (_commands.TryGetValue(id, out var command))
                return command;

            var fromDisk = EditorJobLedger.TryLoad(id);
            if (fromDisk != null)
                _commands[id] = fromDisk;
            return fromDisk;
        }

        public static void Fail(string id, CommandFailure failure)
        {
            var job = Get(id);
            if (job == null || IsFinished(job))
                return;

            Finish(job, InvokeJobStatus.Failed, failure, null);
        }

        public static void MarkRunning(string id)
        {
            var job = Get(id);
            if (job == null || IsFinished(job))
                return;

            job.Status = InvokeJobStatus.Running;
            job.UpdatedAtUtcMs = UtcNowMs();
            Save();
        }

        public static void Succeed(string id, object result)
        {
            var job = Get(id);
            if (job == null || IsFinished(job))
                return;

            Finish(job, InvokeJobStatus.Succeeded, null, result);
        }

        /// <summary>The live job created for a file-queue command, if one exists.</summary>
        public static InvokeJobRecord FindByRequestId(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            if (_commands.TryGetValue(requestId, out var byId))
                return byId;

            // Jobs created before 0.5 have a generated id; only RequestId matches the queue file.
            foreach (var job in _commands.Values)
            {
                if (string.Equals(job.RequestId, requestId, StringComparison.Ordinal))
                    return job;
            }

            return null;
        }

        /// <summary>Finished jobs whose result has not been delivered yet. Polled every frame.</summary>
        public static IReadOnlyList<InvokeJobRecord> FinishedJobs() =>
            _commands.Count == 0
                ? Array.Empty<InvokeJobRecord>()
                : _commands.Values.Where(IsFinished).ToList();

        /// <summary>Forgets a job once its result is delivered.</summary>
        public static void Remove(string id)
        {
            if (string.IsNullOrEmpty(id) || !_commands.Remove(id))
                return;

            EditorJobLedger.Delete(id);
            SaveSession();
        }

        static void Begin(string id, string command, string completionKind, int timeoutMs, string completionArgs)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("A deferred command needs an id.", nameof(id));

            var kind = string.IsNullOrEmpty(completionKind)
                ? InvokeCompletionCatalog.CompletionDeferred
                : completionKind;
            if (kind != InvokeCompletionCatalog.CompletionDeferred && !Policies.ContainsKey(kind))
                throw new InvalidOperationException($"No completion policy for '{kind}'.");

            var now = UtcNowMs();
            if (!_commands.TryGetValue(id, out var job))
            {
                job = new InvokeJobRecord
                {
                    Id = id,
                    RequestId = id,
                    Command = command,
                    Status = InvokeJobStatus.Pending,
                    CreatedAtUtcMs = now,
                };
                _commands[id] = job;
            }

            job.CompletionKind = kind;
            if (completionArgs != null)
                job.CompletionArgs = completionArgs;
            job.UpdatedAtUtcMs = now;
            if (timeoutMs > 0)
                job.DeadlineUtcMs = now + timeoutMs;

            // Synchronously: the domain may reload as soon as the command returns.
            Save();
        }

        /// <summary>SessionState + disk merge after domain reload.</summary>
        static void Reload()
        {
            LoadFromSession();
            EditorJobLedger.MergePendingInto(_commands);
            DropUndeliverable();
            // Anything else on disk is corrupt, or finished in an earlier editor session whose
            // caller is long gone.
            EditorJobLedger.DeleteAllExcept(_commands.Keys);
            OrphanJobsAfterDomainReload();
            Save();
        }

        static void DropUndeliverable()
        {
            var cutoff = UtcNowMs() - FinishedRetentionMs;
            foreach (var job in _commands.Values.ToList())
            {
                if (!IsFinished(job))
                    continue;

                // Before 0.5 (no deadline) a job was delivered as it finished and then kept, so a
                // finished record from then has been delivered already; sending it again only
                // leaves an outbox file nobody reads.
                var writtenBefore05 = job.DeadlineUtcMs == 0;
                if (writtenBefore05 || job.UpdatedAtUtcMs < cutoff)
                    _commands.Remove(job.Id);
            }
        }

        /// <summary>
        /// A job without a completion policy was completed by callbacks that died with the old
        /// domain, so it can never finish now.
        /// </summary>
        static void OrphanJobsAfterDomainReload()
        {
            foreach (var job in _commands.Values.ToList())
            {
                if (IsFinished(job) || Policies.ContainsKey(job.CompletionKind ?? ""))
                    continue;

                Finish(job, InvokeJobStatus.Orphaned, new CommandFailure(UcpErrorCodes.Orphaned, ReloadOrphanError), null);
            }
        }

        static void Tick()
        {
            if (_commands.Count == 0)
                return;

            var now = UtcNowMs();
            var state = EditorStateProvider.Capture();
            foreach (var job in _commands.Values.ToList())
            {
                if (IsFinished(job))
                    continue;

                if (TimedOut(job, now, out var timeoutError))
                {
                    if (job.DeadlineUtcMs > 0)
                    {
                        // ucp-host counts the same timeout from before Unity received the command,
                        // so it has already answered; a result now would only litter the outbox.
                        UcpLog.LogWarning($"[ucp-agent] '{job.Command}' passed its deadline; the caller was told it timed out.");
                        Remove(job.Id);
                        continue;
                    }

                    Finish(job, InvokeJobStatus.Orphaned, new CommandFailure(UcpErrorCodes.Timeout, timeoutError), null);
                    continue;
                }

                if (!Policies.TryGetValue(job.CompletionKind ?? "", out var policy))
                {
                    // CompletionDeferred: the command completes it from a callback.
                    MarkPendingAsRunning(job, now);
                    continue;
                }

                if (policy.TryComplete(job, state, out var result, out var failure))
                {
                    if (failure == null)
                        Finish(job, InvokeJobStatus.Succeeded, null, result);
                    else
                        Finish(job, InvokeJobStatus.Failed, failure, null);
                }
                else
                {
                    MarkPendingAsRunning(job, now);
                }
            }
        }

        static bool TimedOut(InvokeJobRecord job, long now, out string error)
        {
            if (job.DeadlineUtcMs > 0)
            {
                error = TimeoutError;
                return now > job.DeadlineUtcMs;
            }

            error = LegacyTimeoutError;
            return now - job.UpdatedAtUtcMs > LegacyNoProgressTimeoutMs;
        }

        static void MarkPendingAsRunning(InvokeJobRecord job, long now)
        {
            if (job.Status != InvokeJobStatus.Pending)
                return;

            job.Status = InvokeJobStatus.Running;
            job.UpdatedAtUtcMs = now;
            Save();
        }

        /// <param name="result">The success payload; a failure carries its data in <paramref name="failure"/>.</param>
        static void Finish(InvokeJobRecord job, InvokeJobStatus status, CommandFailure failure, object result)
        {
            if (failure != null)
                result = failure.Data;

            job.Status = status;
            job.Error = failure?.Message;
            job.ErrorCode = failure?.Code ?? "";
            job.Hint = failure?.Hint ?? "";
            job.Result = result;
            // Kept as JSON too, so a reload between finishing and delivery does not lose it.
            job.ResultJson = result != null ? UcpJson.Serialize(result) : "";
            job.UpdatedAtUtcMs = UtcNowMs();
            Save();
        }

        static bool IsFinished(InvokeJobRecord job) =>
            job.Status is InvokeJobStatus.Succeeded or InvokeJobStatus.Failed or InvokeJobStatus.Orphaned;

        static long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        static void LoadFromSession()
        {
            _commands = new Dictionary<string, InvokeJobRecord>();
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json))
                return;

            try
            {
                var wrapper = JsonUtility.FromJson<InvokeJobListWrapper>(json);
                foreach (var item in wrapper?.Items ?? new List<InvokeJobRecord>())
                {
                    if (!string.IsNullOrEmpty(item?.Id))
                        _commands[item.Id] = item;
                }
            }
            catch
            {
                _commands = new Dictionary<string, InvokeJobRecord>();
            }
        }

        static void Save()
        {
            if (!MainThread.IsCurrent)
            {
                UcpLog.LogWarning("[ucp-agent] EditorJobStateManager.Save skipped (not main thread).");
                return;
            }

            SaveSession();

            foreach (var job in _commands.Values)
                EditorJobLedger.Persist(job);
        }

        static void SaveSession()
        {
            var wrapper = new InvokeJobListWrapper { Items = _commands.Values.ToList() };
            SessionState.SetString(SessionKey, JsonUtility.ToJson(wrapper));
        }

        sealed class JobStore : IDeferredJobs
        {
            public void Begin(string id, string command, string completionKind, int timeoutMs, string completionArgs = null) =>
                EditorJobStateManager.Begin(id, command, completionKind, timeoutMs, completionArgs);

            public bool Has(string id) => !string.IsNullOrEmpty(id) && _commands.ContainsKey(id);
            public void Remove(string id) => EditorJobStateManager.Remove(id);
            public void MarkRunning(string id) => EditorJobStateManager.MarkRunning(id);
            public void Succeed(string id, object result) => EditorJobStateManager.Succeed(id, result);
            public void Fail(string id, CommandFailure failure) => EditorJobStateManager.Fail(id, failure);
        }
    }
}
