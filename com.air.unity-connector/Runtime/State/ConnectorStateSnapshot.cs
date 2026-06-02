using System.Collections.Generic;

namespace Air.UnityConnector.State
{
    /// <summary>Unified connector + play-mode snapshot for /health and instance heartbeat.</summary>
    public sealed class ConnectorStateSnapshot
    {
        public string ConnectorState;
        public string PlayMode;
        public bool CommandsReady;
        public bool ListenerRunning;
        public string[] BlockingReasons;
        public bool IsPlaying;
        public bool IsPaused;
        public bool IsCompiling;

        public void AppendHealthFields(Dictionary<string, object> payload, bool includeReadyAlias = true)
        {
            payload["connector_state"] = ConnectorState;
            payload["play_mode"] = PlayMode;
            payload["commands_ready"] = CommandsReady;
            if (includeReadyAlias)
                payload["ready"] = CommandsReady;
            payload["listener_running"] = ListenerRunning;
            payload["blocking_reasons"] = BlockingReasons ?? System.Array.Empty<string>();
            payload["is_playing"] = IsPlaying;
            payload["is_paused"] = IsPaused;
            payload["is_compiling"] = IsCompiling;
        }
    }
}
