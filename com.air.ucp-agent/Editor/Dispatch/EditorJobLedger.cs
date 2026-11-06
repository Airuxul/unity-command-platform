using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Job;

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

                var path = Path.Combine(dir, $"{job.Id}.json");
                var tmp = path + ".tmp";
                var json = SerializeOne(job);
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
                        TryDeleteFile(file);
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

        public static void Delete(string commandId)
        {
            if (string.IsNullOrEmpty(commandId))
                return;

            TryDeleteFile(Path.Combine(EditorProjectPaths.JobsDirectory(), $"{commandId}.json"));
        }

        /// <summary>Deletes every job file whose id is not in <paramref name="keepIds"/>.</summary>
        public static void DeleteAllExcept(ICollection<string> keepIds)
        {
            try
            {
                var dir = EditorProjectPaths.JobsDirectory();
                if (!Directory.Exists(dir))
                    return;

                foreach (var file in Directory.GetFiles(dir, "*.json"))
                {
                    if (!keepIds.Contains(Path.GetFileNameWithoutExtension(file)))
                        TryDeleteFile(file);
                }
            }
            catch (Exception ex)
            {
                UcpLog.LogWarning($"[ucp-agent] EditorJobLedger.DeleteAllExcept failed: {ex.Message}");
            }
        }

        static void TryDeleteFile(string path)
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

        public static string SerializeAll(IEnumerable<InvokeJobRecord> jobs) =>
            UcpJson.Serialize(jobs.Select(CopyWithoutLiveResult).ToList());

        static string SerializeOne(InvokeJobRecord job) => UcpJson.Serialize(CopyWithoutLiveResult(job));

        static InvokeJobRecord CopyWithoutLiveResult(InvokeJobRecord job)
        {
            var live = job.Result;
            job.Result = null;
            try
            {
                if (live != null && string.IsNullOrEmpty(job.ResultJson))
                    job.ResultJson = UcpJson.Serialize(live);
                return UcpJson.Deserialize<InvokeJobRecord>(UcpJson.Serialize(job));
            }
            finally
            {
                job.Result = live;
            }
        }

        static InvokeJobRecord ReadFile(string path)
        {
            var json = File.ReadAllText(path);
            var job = UcpJson.Deserialize<InvokeJobRecord>(json);
            if (job == null || string.IsNullOrEmpty(job.Id))
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
    }
}
