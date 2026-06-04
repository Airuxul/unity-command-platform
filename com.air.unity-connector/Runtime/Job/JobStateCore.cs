using System;
using System.Collections.Generic;
using System.Linq;
using Air.GameCore.Serialization;

namespace Air.UnityConnector.Job
{
    public static class JobStateCore
    {
        public delegate bool TryCompleteJob(InvokeJobRecord job, out object result, out string error);

        public const int DefaultOrphanTimeoutMs = 20000;
        public const string OrphanErrorMessage = "Command timed out without progress (20s).";
        public const string ReloadOrphanErrorMessage =
            "Command lost after domain reload (no completion policy to resume).";

        public static InvokeJobRecord Create(
            IDictionary<string, InvokeJobRecord> jobs,
            string command,
            string completionKind,
            string requestId,
            Func<long> utcNow,
            Action save)
        {
            var now = utcNow();
            var entry = new InvokeJobRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Command = command,
                RequestId = requestId,
                CompletionKind = completionKind,
                Status = InvokeJobStatus.Pending,
                CreatedAtUtcMs = now,
                UpdatedAtUtcMs = now,
            };
            jobs[entry.Id] = entry;
            save?.Invoke();
            return entry;
        }

        public static bool ShouldSkip(InvokeJobRecord job) =>
            job.Status is InvokeJobStatus.Succeeded or InvokeJobStatus.Failed or InvokeJobStatus.Orphaned;

        public static bool TryOrphan(InvokeJobRecord job, long nowUtcMs, int orphanTimeoutMs, Action save)
        {
            if (nowUtcMs - job.UpdatedAtUtcMs <= orphanTimeoutMs) return false;
            return MarkOrphaned(job, OrphanErrorMessage, nowUtcMs, save);
        }

        public static bool MarkOrphaned(InvokeJobRecord job, string error, long nowUtcMs, Action save)
        {
            if (ShouldSkip(job))
                return false;

            job.Status = InvokeJobStatus.Orphaned;
            job.Error = error ?? OrphanErrorMessage;
            job.UpdatedAtUtcMs = nowUtcMs;
            save?.Invoke();
            return true;
        }

        public static void MarkFailed(InvokeJobRecord job, string error, long nowUtcMs, Action save)
        {
            job.Status = InvokeJobStatus.Failed;
            job.Error = error;
            job.UpdatedAtUtcMs = nowUtcMs;
            save?.Invoke();
        }

        public static void MarkSucceeded(InvokeJobRecord job, object result, long nowUtcMs, bool serializeResultJson, Action save)
        {
            job.Status = InvokeJobStatus.Succeeded;
            job.Result = result;
            if (serializeResultJson) job.ResultJson = result != null ? JsonHost.Serialize(result) : "";
            job.UpdatedAtUtcMs = nowUtcMs;
            save?.Invoke();
        }

        public static void TickJobs(
            IEnumerable<InvokeJobRecord> jobs,
            Action save,
            Func<InvokeJobRecord, TryCompleteJob> resolveCompletion,
            Func<InvokeJobRecord, string> resolveMissingPolicyError,
            Func<InvokeJobRecord, bool> canRunWithoutPolicy,
            bool serializeResultJson)
        {
            var now = UtcNowMs();
            foreach (var job in jobs.ToList())
            {
                if (ShouldSkip(job)) continue;
                if (TryOrphan(job, now, DefaultOrphanTimeoutMs, save)) continue;
                var tryComplete = resolveCompletion(job);
                if (tryComplete == null)
                {
                    if (canRunWithoutPolicy != null && canRunWithoutPolicy(job))
                    {
                        if (job.Status == InvokeJobStatus.Pending)
                        {
                            job.Status = InvokeJobStatus.Running;
                            job.UpdatedAtUtcMs = now;
                            save?.Invoke();
                        }

                        continue;
                    }

                    MarkFailed(job, resolveMissingPolicyError(job), now, save);
                    continue;
                }

                if (tryComplete(job, out var result, out var error))
                {
                    if (!string.IsNullOrEmpty(error)) MarkFailed(job, error, now, save);
                    else MarkSucceeded(job, result, now, serializeResultJson, save);
                }
                else if (job.Status == InvokeJobStatus.Pending)
                {
                    job.Status = InvokeJobStatus.Running;
                    job.UpdatedAtUtcMs = now;
                    save?.Invoke();
                }
            }
        }

        public static long UtcNowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }
}
