using Air.UnityConnector.Job;
using System.Collections.Generic;
using Air.UnityConnector.Editor.Services;
using UnityEditor;
using Air.UnityConnector;
using Air.UnityConnector.Server;
using Air.UnityConnector.State;

namespace Air.UnityConnector
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
                ReadyForTools = !compiling,
            };
        }

        public static Dictionary<string, object> ToManifestObject()
        {
            var state = Capture();
            var connector = CaptureConnectorState();
            var activeCommand = FindActiveCommandId();
            var manifest = new Dictionary<string, object>
            {
                ["connector_state"] = connector.ConnectorState,
                ["play_mode"] = connector.PlayMode,
                ["commands_ready"] = connector.CommandsReady,
                ["is_playing"] = state.IsPlaying,
                ["is_paused"] = state.IsPaused,
                ["is_compiling"] = state.IsCompiling,
                ["ready_for_tools"] = state.ReadyForTools,
                ["blocking_reasons"] = connector.BlockingReasons,
                ["active_command"] = activeCommand,
                ["connector_build"] = ConnectorBuild.Id,
                ["supervisor_phase"] = EditorServerSupervisor.Instance.Phase.ToString(),
                ["startup_failure"] = BuildStartupFailureObject(),
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

        private static Dictionary<string, object> BuildStartupFailureObject()
        {
            if (string.IsNullOrEmpty(EditorConnectorStartupLog.LastMessage))
                return null;

            return new Dictionary<string, object>
            {
                ["site"] = EditorConnectorStartupLog.LastSite,
                ["message"] = EditorConnectorStartupLog.LastMessage,
                ["utc"] = EditorConnectorStartupLog.LastUtc,
            };
        }

        private static string FindActiveCommandId()
        {
            foreach (var pair in EditorJobStateManager.AllCommands)
            {
                var command = pair.Value;
                if (command.Status is InvokeJobStatus.Pending or InvokeJobStatus.Running)
                    return command.Id;
            }

            return null;
        }

        internal static ConnectorStateSnapshot CaptureConnectorState(string forcedConnectorState = null)
        {
            var inputs = new ConnectorStateInputs
            {
                ForcedConnectorState = forcedConnectorState,
                ListenerActive = EditorHttpSession.ListenerActive,
                DomainReloading = EditorHttpSession.DomainReloading,
                IsCompiling = EditorApplication.isCompiling,
                IsUpdating = EditorApplication.isUpdating,
                IsPlaying = EditorApplication.isPlaying,
                IsPaused = EditorApplication.isPlaying && EditorApplication.isPaused,
                IsCommandReady = EditorHttpSession.IsCommandReady,
            };
            return ConnectorStateEvaluator.Evaluate(inputs);
        }

        /// <summary>
        /// Thread-safe snapshot for /health worker threads.
        /// Never read EditorApplication directly here.
        /// </summary>
        internal static ConnectorStateSnapshot CaptureConnectorStateForHealth()
        {
            var inputs = new ConnectorStateInputs
            {
                ListenerActive = EditorHttpSession.ListenerActive,
                DomainReloading = EditorHttpSession.DomainReloading,
                IsCompiling = EditorPlayState.IsCompiling,
                IsUpdating = EditorPlayState.IsUpdating,
                IsPlaying = EditorPlayState.IsPlaying,
                IsPaused = EditorPlayState.IsPaused,
                IsCommandReady = EditorHttpSession.IsCommandReady,
            };
            return ConnectorStateEvaluator.Evaluate(inputs);
        }

        internal static string[] BuildBlockingReasonsPublic(EditorStateSnapshot state) =>
            CaptureConnectorState().BlockingReasons;
    }
}
