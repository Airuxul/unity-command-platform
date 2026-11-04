using System;
using System.Collections.Generic;
using System.IO;
using Air.UcpAgent.Dispatch;
using Air.UcpAgent.Editor.Bridge;
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
        static readonly CommandHandlerRegistry Registry = CreateRegistry();
        static string _projectId;
        static double _nextPoll;
        static double _nextHeartbeat;
        const double PollIntervalSec = 0.25;
        const double HeartbeatIntervalSec = 2.0;

        static EditorAgentBootstrap()
        {
            _projectId = ProjectId.FromPath(GetProjectPath());
            UcpPaths.EnsureDirectory(UcpPaths.InboxDir(_projectId));
            UcpPaths.EnsureDirectory(UcpPaths.OutboxDir(_projectId));
            WriteSession("ready");
            EditorApplication.update += OnUpdate;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterReload;
            EditorApplication.quitting += OnQuitting;
        }

        static CommandHandlerRegistry CreateRegistry()
        {
            var registry = new CommandHandlerRegistry();
            UcpCommandRegistrar.RegisterEditorCommands(registry);
            return registry;
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
                capabilities = new List<string>(Registry.Capabilities),
                updatedAt = DateTime.UtcNow.ToString("o"),
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
                UcpCommand command;
                try
                {
                    command = JsonConvert.DeserializeObject<UcpCommand>(File.ReadAllText(file));
                }
                catch (JsonException ex)
                {
                    Debug.LogWarning("[ucp-agent] dropping unreadable inbox command: " + ex.Message);
                    TryDelete(file);
                    continue;
                }
                catch (IOException)
                {
                    continue;
                }

                if (command == null || string.IsNullOrEmpty(command.id))
                {
                    TryDelete(file);
                    continue;
                }

                // An inbox file is only ever executed once. Before this, a deferred command's file
                // stayed in the inbox, and the first poll after a domain reload ran it again.
                var outbox = UcpPaths.OutboxFile(_projectId, command.id);
                if (!File.Exists(outbox) && EditorJobStateManager.FindByRequestId(command.id) == null)
                {
                    if (TryExecute(command, out var result) && result != null)
                        WriteOutbox(command.id, result);
                }

                TryDelete(file);
            }
        }

        /// <summary>Writes the outbox of every deferred job that has finished, then forgets the job.</summary>
        static void DeliverFinishedJobs()
        {
            foreach (var job in EditorJobStateManager.FinishedJobs())
            {
                try
                {
                    if (!string.IsNullOrEmpty(job.RequestId))
                        WriteOutbox(job.RequestId, UcpResultMapper.FromJob(job.RequestId, job));
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

        static bool TryExecute(UcpCommand command, out UcpResult result)
        {
            result = null;
            if (!Registry.Dispatcher.TryGetHandler(command.type, out var handler))
                return Registry.Dispatcher.TryDispatch(command, out result);

            if (handler is UcpCliCommandHandler bridge)
            {
                var execution = bridge.Dispatch(command);
                if (execution.IsDeferred)
                    return false;

                result = execution.Result;
                return result != null;
            }

            return Registry.Dispatcher.TryDispatch(command, out result);
        }
    }
}
