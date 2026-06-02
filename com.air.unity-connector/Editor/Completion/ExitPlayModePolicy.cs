using Air.UnityConnector.Job;
using Air.UnityConnector.Invoke;
using Air.UnityConnector;
namespace Air.UnityConnector.Completion
{
    public sealed class ExitPlayModePolicy : ICompletionPolicy
    {
        public string Kind => InvokeCompletionCatalog.CompletionExitPlay;

        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out string error)
        {
            result = null;
            error = null;

            if (!state.IsPlaying)
            {
                result = new System.Collections.Generic.Dictionary<string, object> { ["is_playing"] = false };
                return true;
            }

            if (command.Status == InvokeJobStatus.Pending)
                command.Status = InvokeJobStatus.Running;

            return false;
        }
    }
}
