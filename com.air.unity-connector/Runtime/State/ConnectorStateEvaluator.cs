using System;

namespace Air.UnityConnector.State
{
    /// <summary>Single source of truth for connector_state / play_mode / commands_ready.</summary>
    public static class ConnectorStateEvaluator
    {
        public static ConnectorStateSnapshot Evaluate(ConnectorStateInputs inputs)
        {
            inputs ??= new ConnectorStateInputs();
            var connectorState = ResolveConnectorState(inputs);
            var playMode = ResolvePlayMode(inputs);
            var commandsReady = AreCommandsReady(inputs, connectorState);

            return new ConnectorStateSnapshot
            {
                ConnectorState = connectorState,
                PlayMode = playMode,
                CommandsReady = commandsReady,
                ListenerRunning = inputs.ListenerActive,
                BlockingReasons = BuildBlockingReasons(inputs, connectorState),
                IsPlaying = inputs.IsPlaying,
                IsPaused = inputs.IsPaused,
                IsCompiling = inputs.IsCompiling,
            };
        }

        static string ResolveConnectorState(ConnectorStateInputs inputs)
        {
            if (!string.IsNullOrEmpty(inputs.ForcedConnectorState))
                return inputs.ForcedConnectorState;

            if (inputs.DomainReloading || !inputs.ListenerActive)
                return ConnectorPipelineState.Reloading;
            if (inputs.IsCompiling)
                return ConnectorPipelineState.Compiling;
            if (inputs.IsUpdating)
                return ConnectorPipelineState.Refreshing;
            return ConnectorPipelineState.Ready;
        }

        static string ResolvePlayMode(ConnectorStateInputs inputs)
        {
            if (!inputs.IsPlaying)
                return PlayModeState.Edit;
            return inputs.IsPaused ? PlayModeState.Paused : PlayModeState.Playing;
        }

        static bool AreCommandsReady(ConnectorStateInputs inputs, string connectorState)
        {
            if (connectorState != ConnectorPipelineState.Ready)
                return false;

            return inputs.IsCommandReady
                && !inputs.IsCompiling
                && !inputs.IsUpdating;
        }

        static string[] BuildBlockingReasons(ConnectorStateInputs inputs, string connectorState)
        {
            if (connectorState == ConnectorPipelineState.Reloading
                || inputs.DomainReloading
                || !inputs.ListenerActive)
            {
                return new[] { ConnectorPipelineState.Reloading };
            }

            if (connectorState == ConnectorPipelineState.Compiling || inputs.IsCompiling)
                return new[] { ConnectorPipelineState.Compiling };

            if (connectorState == ConnectorPipelineState.Refreshing || inputs.IsUpdating)
                return new[] { ConnectorPipelineState.Refreshing };

            return Array.Empty<string>();
        }
    }
}
