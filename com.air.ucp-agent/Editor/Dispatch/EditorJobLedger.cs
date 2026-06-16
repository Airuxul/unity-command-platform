using System;
using System.Collections.Generic;
using System.IO;
using Air.UcpAgent.Job;
using UnityEngine;

namespace Air.UcpAgent
{
    /// <summary>
    /// Durable editor deferred-command store (~/.ucp/jobs/{project}/{{id}}.json).
    /// Survives domain reload; <see cref="EditorJobStateManager"/> mirrors it in memory.
    /// </summary>
    internal static class EditorJobLedger
    {
        static bool IsTerminal(InvokeJobRecord job) =>
            job.Status is InvokeJobStatus.Succeeded or InvokeJobStatus.Failed or InvokeJobStatus.Orphaned;

        public static void Persist(InvokeJobRecord job)
        {
            if (job == null || string.IsNullOrEmpty(job.Id))
                return;

            try
            {
                var dir = EditorProjectPaths.JobsDirectory();
                Directory.CreateDirectory(dir);

                var snapshot = Snapshot(job);
                var path = Path.Combine(dir, $"{job.Id}.json");
                var tmp = path + ".tmp";
                var json = JsonUtility.ToJson(new InvokeJobListWrapper
                {
                    Items = new List<InvokeJobRecord> { snapshot },
                });
                File.WriteAllText(tmp, json);
                if (File.Exists(path))
                    File.Replace(tmp, path, null);
                else
                    File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                UcpLog.LogWarning($"[ucp-agent] EditorJobLedger.Persist failed: {ex.Message}");
            }
        }

        public static InvokeJobRecord TryLoad(string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
                return null;

            try
            {
                var path = Path.Combine(EditorProjectPaths.JobsDirectory(), $"{commandId}.json");
                if (!File.Exists(path))
                    return null;
                return ReadFile(path);
            }
            catch
            {
                return null;
            }
        }

        public static void MergePendingInto(IDictionary<string, InvokeJobRecord> commands)
        {
            if (commands == null)
                return;

            try
            {
                var dir = EditorProjectPaths.JobsDirectory();
                if (!Directory.Exists(dir))
                    return;

                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    var job = ReadFile(file);
                    if (job == null || string.IsNullOrEmpty(job.Id))
                    {
                        TryDeleteCorruptFile(file);
                        continue;
                    }

                    if (IsTerminal(job))
                    {
                        continue;
                    }

                    if (!commands.TryGetValue(job.Id, out var existing)
                        || job.UpdatedAtUtcMs >= existing.UpdatedAtUtcMs)
                    {
                        commands[job.Id] = job;
                    }
                }
            }
            catch (Exception ex)
            {
                UcpLog.LogWarning($"[ucp-agent] EditorJobLedger.MergePendingInto failed: {ex.Message}");
            }
        }

        public static void PurgeCorruptFiles()
        {
            try
            {
                var dir = EditorProjectPaths.JobsDirectory();
                if (!Directory.Exists(dir))
                    return;

                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    var job = ReadFile(file);
                    if (job == null || string.IsNullOrEmpty(job.Id))
                        TryDeleteCorruptFile(file);
                }
            }
            catch (Exception ex)
            {
                UcpLog.LogWarning($"[ucp-agent] EditorJobLedger.PurgeCorruptFiles failed: {ex.Message}");
            }
        }

        static void TryDeleteCorruptFile(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // ignored
            }
        }

        public static void FlushAll(IEnumerable<InvokeJobRecord> jobs)
        {
            if (jobs == null)
                return;

            foreach (var job in jobs)
                Persist(job);
        }

        static InvokeJobRecord ReadFile(string path)
        {
            var json = File.ReadAllText(path);
            var wrapper = JsonUtility.FromJson<InvokeJobListWrapper>(json);
            var job = wrapper?.Items != null && wrapper.Items.Count > 0 ? wrapper.Items[0] : null;
            if (job == null)
                return null;

            if (job.Result == null && !string.IsNullOrEmpty(job.ResultJson))
            {
                try
                {
                    job.Result = UcpJson.Deserialize(job.ResultJson);
                }
                catch
                {
                    // ignored
                }
            }

            return job;
        }

        static InvokeJobRecord Snapshot(InvokeJobRecord job)
        {
            var copy = new InvokeJobRecord
            {
                Id = job.Id,
                Command = job.Command,
                RequestId = job.RequestId,
                Status = job.Status,
                CompletionKind = job.CompletionKind,
                Error = job.Error,
                CreatedAtUtcMs = job.CreatedAtUtcMs,
                UpdatedAtUtcMs = job.UpdatedAtUtcMs,
                ResultJson = job.ResultJson,
            };

            if (job.Result != null)
            {
                try
                {
                    copy.ResultJson = UcpJson.Serialize(job.Result);
                }
                catch
                {
                    copy.ResultJson = job.ResultJson ?? "";
                }
            }

            return copy;
        }
    }
}
