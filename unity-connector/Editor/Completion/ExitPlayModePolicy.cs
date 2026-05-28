namespace UnityCliConnector.Completion
{
    public sealed class ExitPlayModePolicy : ICompletionPolicy
    {
        public string Kind => CommandCompletionCatalog.CompletionExitPlay;

        public bool TryComplete(CommandRecord command, EditorStateSnapshot state, out object result, out string error)
        {
            result = null;
            error = null;

            if (!state.IsPlaying)
            {
                result = new System.Collections.Generic.Dictionary<string, object> { ["is_playing"] = false };
                return true;
            }

            if (command.Status == CommandStatus.Pending)
                command.Status = CommandStatus.Running;

            return false;
        }
    }
}
