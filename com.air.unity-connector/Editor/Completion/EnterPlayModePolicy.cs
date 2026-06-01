namespace UnityCliConnector.Completion
{
    public sealed class EnterPlayModePolicy : ICompletionPolicy
    {
        public string Kind => CommandCompletionCatalog.CompletionEnterPlay;

        public bool TryComplete(CommandRecord command, EditorStateSnapshot state, out object result, out string error)
        {
            result = null;
            error = null;

            if (state.IsPlaying)
            {
                result = new System.Collections.Generic.Dictionary<string, object> { ["is_playing"] = true };
                return true;
            }

            if (command.Status == CommandStatus.Pending)
                command.Status = CommandStatus.Running;

            return false;
        }
    }
}
