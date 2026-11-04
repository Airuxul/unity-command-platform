using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.IO;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Air.UcpAgent.Editor
{
    [InitializeOnLoad]
    public static class EditorAgentBootstrap
    {
        static readonly List<string> Capabilities =
            CommandRegistry.Capabilities(CommandRegistry.EditorHost, includeAliases: true);

        static readonly Dictionary<string, int> DefaultTimeouts = BuildDefaultTimeouts();
        static readonly int Pid = System.Diagnostics.Process.GetCurrentProcess().Id;

        static string _projectId;
        static double _nextPoll;
        static double _nextHeartbeat;
        // Every tick in the background (about 10 a second), at most 50 a second in front. Was 0.25 s,
        // which added about 125 ms to every command; listing an empty folder costs microseconds.
        const double PollIntervalSec = 0.02;
        const double HeartbeatIntervalSec = 2.0;

        static EditorAgentBootstrap()
        {
            _projectId = ProjectId.FromPath(GetProjectPath());
            UcpPaths.EnsureDirectory(UcpPaths.InboxDir(_projectId));
            UcpPaths.EnsureDirectory(UcpPaths.OutboxDir(_projectId));
            RemoveLeftoverClaims();
            WriteSession("ready");
            EditorApplication.update += OnUpdate;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
            EditorApplication.quitting += OnQuitting;
        }

        static string GetProjectPath() => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        static void OnAfterReload()
        {
            _projectId = ProjectId.FromPath(GetProjectPath());
            WriteSession("ready");
        }

        static void OnQuitting() => WriteSession("offline");

        static void OnUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now >= _nextHeartbeat)
            {
                _nextHeartbeat = now + HeartbeatIntervalSec;
                WriteSession("ready");
            }

            // Also loads the job store, so a job restored after a reload is known before the first
            // poll could run its inbox file again.
            DeliverFinishedJobs();

            if (now < _nextPoll)
                return;
            _nextPoll = now + PollIntervalSec;
            PollInbox();
        }

        static void WriteSession(string status)
        {
            var session = new UcpSession
            {
                id = _projectId,
                name = Application.productName,
                path = GetProjectPath().Replace('\\', '/'),
                type = "editor",
                status = status,
                capabilities = new List<string>(Capabilities),
                updatedAt = DateTime.UtcNow.ToString("o"),
                pid = Pid,
                focused = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
                defaultTimeouts = DefaultTimeouts,
            };
            UcpPaths.WriteJsonAtomic(
                UcpPaths.SessionFile(_projectId),
                JsonConvert.SerializeObject(session, Formatting.Indented));
        }

        static void PollInbox()
        {
            var inbox = UcpPaths.InboxDir(_projectId);
            if (!Directory.Exists(inbox))
                return;

            string[] files;
            try
            {
                files = Directory.GetFiles(inbox, "*.json");
            }
            catch
            {
                return;
            }

            if (files.Length == 0)
                return;

            Array.Sort(files, StringComparer.Ordinal);

            foreach (var file in files)
            {
                // Claimed by renaming: ucp-host withdraws a timed-out command by deleting x.json,
                // so exactly one of the two wins and a withdrawn command never runs.
                var claimed = Path.ChangeExtension(file, ClaimedExtension);
                try
                {
                    File.Move(file, claimed);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Withdrawn, or still being written; the next poll sees which.
                    continue;
                }

                UcpCommand command;
                try
                {
                    command = JsonConvert.DeserializeObject<UcpCommand>(File.ReadAllText(claimed));
                }
                catch (Exception ex) when (ex is JsonException || ex is IOException)
                {
                    Debug.LogWarning("[ucp-agent] dropping unreadable inbox command: " + ex.Message);
                    TryDelete(claimed);
                    continue;
                }

                if (command == null || string.IsNullOrEmpty(command.id))
                {
                    TryDelete(claimed);
                    continue;
                }

                // An inbox file is only ever executed once. Before this, a deferred command's file
                // stayed in the inbox, and the first poll after a domain reload ran it again.
                var outbox = UcpPaths.OutboxFile(_projectId, command.id);
                if (!File.Exists(outbox) && EditorJobStateManager.FindByRequestId(command.id) == null)
                {
                    var result = CommandExecutor.Execute(
                        CommandRegistry.EditorHost, command, EditorJobStateManager.Jobs);
                    // Null: the command continues as a job, delivered by DeliverFinishedJobs.
                    if (result != null)
                        WriteOutbox(command.id, result);
                }

                TryDelete(claimed);
            }
        }

        const string ClaimedExtension = ".claimed";

        /// <summary>
        /// A claim outlives its command only if Unity died mid-command: a domain reload never lands
        /// inside PollInbox. Such a command is not run again.
        /// </summary>
        static void RemoveLeftoverClaims()
        {
            try
            {
                foreach (var file in Directory.GetFiles(UcpPaths.InboxDir(_projectId), "*" + ClaimedExtension))
                    TryDelete(file);
            }
            catch (IOException)
            {
                // The host's stale-file sweep removes them later.
            }
        }

        static Dictionary<string, int> BuildDefaultTimeouts()
        {
            var table = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in CommandRegistry.ForHost(CommandRegistry.EditorHost))
            {
                if (entry.DefaultTimeoutMs <= 0)
                    continue;

                foreach (var key in new[] { entry.Name }.Concat(entry.Aliases))
                {
                    if (!string.IsNullOrEmpty(key))
                        table[key] = entry.DefaultTimeoutMs;
                }
            }

            return table;
        }

        /// <summary>Writes the outbox of every deferred job that has finished, then forgets the job.</summary>
        static void DeliverFinishedJobs()
        {
            foreach (var job in EditorJobStateManager.FinishedJobs())
            {
                try
                {
                    if (!string.IsNullOrEmpty(job.RequestId))
                        WriteOutbox(job.RequestId, UcpResults.FromJob(job.RequestId, job));
                }
                catch (Exception ex)
                {
                    // Kept, so the next frame tries again.
                    Debug.LogWarning($"[ucp-agent] could not deliver result of '{job.Command}': {ex.Message}");
                    continue;
                }

                EditorJobStateManager.Remove(job.Id);
            }
        }

        static void WriteOutbox(string commandId, UcpResult result) =>
            UcpPaths.WriteJsonAtomic(
                UcpPaths.OutboxFile(_projectId, commandId),
                JsonConvert.SerializeObject(result, Formatting.Indented));

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
                // Retried on the next poll.
            }
            catch (UnauthorizedAccessException)
            {
                // Retried on the next poll.
            }
        }
    }
}
