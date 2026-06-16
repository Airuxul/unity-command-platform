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

            if (PendingDeferredCommands.HasPending)
                PendingDeferredCommands.Tick();

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
                catch (Exception ex)
                {
                    Debug.LogWarning("[ucp-agent] failed to read inbox command: " + ex.Message);
                    continue;
                }

                if (command == null || string.IsNullOrEmpty(command.id))
                    continue;

                var outbox = UcpPaths.OutboxFile(_projectId, command.id);
                if (File.Exists(outbox))
                    continue;

                if (!TryExecute(command, out var result) || result == null)
                    continue;

                UcpPaths.WriteJsonAtomic(outbox, JsonConvert.SerializeObject(result, Formatting.Indented));
            }
        }

        static bool TryExecute(UcpCommand command, out UcpResult result)
        {
            result = null;
            if (!Registry.Dispatcher.TryGetHandler(command.type, out var handler))
                return Registry.Dispatcher.TryDispatch(command, out result);

            if (handler is UcpCliCommandHandler bridge)
            {
                var execution = bridge.Execute(command, _projectId);
                if (execution.IsDeferred)
                    return false;

                result = execution.Result;
                return result != null;
            }

            return Registry.Dispatcher.TryDispatch(command, out result);
        }
    }
}
