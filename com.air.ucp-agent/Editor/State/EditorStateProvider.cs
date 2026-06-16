using System.Collections.Generic;
using Air.UcpAgent.Editor.Services;
using Air.UcpAgent.Job;
using UnityEditor;

namespace Air.UcpAgent
{
    public static class EditorStateProvider
    {
        public static EditorStateSnapshot Capture()
        {
            var compiling = EditorApplication.isCompiling;
            var playing = EditorApplication.isPlaying;
            return new EditorStateSnapshot
            {
                IsCompiling = compiling,
                IsPlaying = playing,
                IsPaused = playing && EditorApplication.isPaused,
                ReadyForTools = !compiling && !EditorApplication.isUpdating,
            };
        }

        public static Dictionary<string, object> ToManifestObject()
        {
            var state = Capture();
            var playing = state.IsPlaying;
            var playMode = !playing ? "edit" : state.IsPaused ? "paused" : "playing";
            var agentState = state.ReadyForTools ? "ready" : state.IsCompiling ? "compiling" : "busy";
            var activeCommand = FindActiveCommandId();

            var manifest = new Dictionary<string, object>
            {
                ["agent_state"] = agentState,
                ["play_mode"] = playMode,
                ["commands_ready"] = state.ReadyForTools,
                ["is_playing"] = state.IsPlaying,
                ["is_paused"] = state.IsPaused,
                ["is_compiling"] = state.IsCompiling,
                ["ready_for_tools"] = state.ReadyForTools,
                ["blocking_reasons"] = state.ReadyForTools
                    ? System.Array.Empty<string>()
                    : new[] { state.IsCompiling ? "compiling" : "busy" },
                ["active_command"] = activeCommand,
                ["ucp_agent_build"] = UcpAgentBuild.Id,
            };

            if (UnityConsoleReader.IsReady)
            {
                var entries = UnityConsoleReader.Read(new UnityConsoleReader.ConsoleReadOptions
                {
                    TypeFilter = "error,warning",
                    MaxEntries = 5,
                    Stacktrace = "none",
                });
                manifest["recent_console"] = entries;
                manifest["recent_console_count"] = entries.Count;
            }
            else
            {
                manifest["recent_console"] = new List<Dictionary<string, object>>();
                manifest["recent_console_count"] = 0;
            }

            return manifest;
        }

        static string FindActiveCommandId()
        {
            foreach (var pair in EditorJobStateManager.AllCommands)
            {
                var command = pair.Value;
                if (command.Status is InvokeJobStatus.Pending or InvokeJobStatus.Running)
                    return command.Id;
            }

            return null;
        }
    }
}
