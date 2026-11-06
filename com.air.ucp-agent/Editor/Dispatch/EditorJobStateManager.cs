using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using UnityEditor;

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

        /// <summary>A finished job older than this has no caller left (ucp-host drops results after 10 min).</summary>
        const long FinishedRetentionMs = 10 * 60_000;

        const string ReloadOrphanError =
            "The completion policy class is not loaded, so this command cannot resume.";

        static readonly Dictionary<string, ICompletionPolicy> Policies = DiscoverPolicies();

        static Dictionary<string, InvokeJobRecord> _commands = new();

        /// <summary>The <see cref="IDeferredJobs"/> the command executor uses on the Editor host.</summary>
        public static readonly IDeferredJobs Jobs = new JobStore();

        static EditorJobStateManager()
        {
            ValidateCompletions();
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

        /// <summary>
        /// Finished jobs whose result has not been delivered yet. Polled every frame. A group's step
        /// is not among them: its group reads it.
        /// </summary>
        public static IReadOnlyList<InvokeJobRecord> FinishedJobs() =>
            _commands.Count == 0
                ? Array.Empty<InvokeJobRecord>()
                : _commands.Values.Where(job => IsFinished(job) && string.IsNullOrEmpty(job.ParentId)).ToList();

        /// <summary>Forgets a job once its result is delivered.</summary>
        public static void Remove(string id)
        {
            if (string.IsNullOrEmpty(id) || !_commands.Remove(id))
                return;

            EditorJobLedger.Delete(id);
            SaveSession();
        }

        static void Begin(string id, string command, string completionKind, int timeoutMs, string completionArgs, string parentId)
        {
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("A deferred command needs an id.", nameof(id));

            if (string.IsNullOrEmpty(completionKind))
                throw new InvalidOperationException("A deferred command must name its completion policy.");
            if (timeoutMs <= 0)
                throw new InvalidOperationException($"'{command}' needs a deadline.");

            var kind = completionKind;
            if (!Policies.ContainsKey(kind))
                throw new InvalidOperationException($"No completion policy '{kind}'.");

            var now = UtcNowMs();
            if (!_commands.TryGetValue(id, out var job))
            {
                job = new InvokeJobRecord
                {
                    Id = id,
                    Command = command,
                    Status = InvokeJobStatus.Pending,
                    CreatedAtUtcMs = now,
                };
                _commands[id] = job;
            }

            job.CompletionKind = kind;
            if (parentId != null)
                job.ParentId = parentId;
            if (completionArgs != null)
                job.CompletionArgs = completionArgs;
            job.UpdatedAtUtcMs = now;
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

                if (job.UpdatedAtUtcMs < cutoff)
                    _commands.Remove(job.Id);
            }
        }

        /// <summary>
        /// The policy class named on the job is not loaded in this domain, so nothing can resume it.
        /// </summary>
        static void Orphan(InvokeJobRecord job)
        {
            if (IsFinished(job))
                return;

            Finish(job, InvokeJobStatus.Orphaned, new CommandFailure(UcpErrorCodes.Orphaned, ReloadOrphanError), null);
        }

        static void OrphanJobsAfterDomainReload()
        {
            foreach (var job in _commands.Values.ToList())
            {
                if (IsFinished(job) || Policies.ContainsKey(job.CompletionKind ?? ""))
                    continue;

                Orphan(job);
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

                if (job.DeadlineUtcMs > 0 && now > job.DeadlineUtcMs)
                {
                    // ucp-host counts the same timeout from before Unity received the command,
                    // so it has already answered; a result now would only litter the outbox.
                    UcpLog.LogWarning(
                        $"[ucp-agent] '{job.Command}' ({job.Id}, completion {job.CompletionKind ?? "none"}) passed its deadline; the caller was told it timed out.");
                    if (Policies.TryGetValue(job.CompletionKind ?? "", out var expiring) && expiring is IExpiringPolicy onExpired)
                    {
                        try
                        {
                            onExpired.Expired(job);
                        }
                        catch (Exception ex)
                        {
                            UcpLog.LogWarning($"[ucp-agent] '{job.Command}' ({job.Id}) could not clean up after its deadline: {ex.Message}");
                        }
                    }

                    Remove(job.Id);
                    continue;
                }

                if (!Policies.TryGetValue(job.CompletionKind ?? "", out var policy))
                {
                    Orphan(job);
                    continue;
                }

                if (policy.TryComplete(job, state, out var result, out var failure))
                {
                    if (failure == null)
                        Finish(job, InvokeJobStatus.Succeeded, null, result);
                    else
                        Finish(job, InvokeJobStatus.Failed, failure, result);
                }
                else
                {
                    MarkPendingAsRunning(job, now);
                }
            }
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
            if (result is PolicyOutcome outcome)
            {
                job.Message = outcome.Message ?? "";
                result = outcome.Data;
            }

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

        public static bool IsFinished(InvokeJobRecord job) =>
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
                var items = UcpJson.Deserialize<List<InvokeJobRecord>>(json);
                foreach (var item in items ?? new List<InvokeJobRecord>())
                {
                    if (string.IsNullOrEmpty(item?.Id))
                        continue;

                    RestoreResult(item);
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
            SessionState.SetString(SessionKey, EditorJobLedger.SerializeAll(_commands.Values));
        }

        static void RestoreResult(InvokeJobRecord job)
        {
            if (job.Result != null || string.IsNullOrEmpty(job.ResultJson))
                return;

            try
            {
                job.Result = UcpJson.Deserialize(job.ResultJson);
            }
            catch
            {
                // The payload is gone; delivery still reports the status.
            }
        }

        static Dictionary<string, ICompletionPolicy> DiscoverPolicies()
        {
            var map = new Dictionary<string, ICompletionPolicy>(StringComparer.Ordinal);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var type in types)
                {
                    if (type == null || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters
                        || !typeof(ICompletionPolicy).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    var policy = (ICompletionPolicy)Activator.CreateInstance(type);
                    var name = type.FullName;
                    if (map.ContainsKey(name))
                        throw new InvalidOperationException($"Duplicate completion policy '{name}'.");

                    map[name] = policy;
                }
            }

            return map;
        }

        static void ValidateCompletions()
        {
            foreach (var entry in CommandRegistry.Entries)
            {
                var kind = entry.Completion;
                if (string.IsNullOrEmpty(kind))
                    continue;
                if (!Policies.ContainsKey(kind))
                {
                    throw new InvalidOperationException(
                        $"Command '{entry.Name}' completes as '{kind}', which has no policy.");
                }
            }
        }

        sealed class JobStore : IDeferredJobs
        {
            public void Begin(string id, string command, string completionKind, int timeoutMs, string completionArgs = null, string parentId = null) =>
                EditorJobStateManager.Begin(id, command, completionKind, timeoutMs, completionArgs, parentId);

            public bool Has(string id) => !string.IsNullOrEmpty(id) && _commands.ContainsKey(id);
            public void Remove(string id) => EditorJobStateManager.Remove(id);
            public void MarkRunning(string id) => EditorJobStateManager.MarkRunning(id);
            public void Succeed(string id, object result) => EditorJobStateManager.Succeed(id, result);
            public void Fail(string id, CommandFailure failure) => EditorJobStateManager.Fail(id, failure);
        }
    }
}
