using Air.UcpAgent.Job;
using Air.UcpAgent.Invoke;
using Air.UcpAgent;
namespace Air.UcpAgent.Completion
{
    public sealed class EnterPlayModePolicy : ICompletionPolicy
    {
        public string Kind => InvokeCompletionCatalog.CompletionEnterPlay;

        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out string error)
        {
            result = null;
            error = null;

            if (state.IsPlaying)
            {
                result = new System.Collections.Generic.Dictionary<string, object> { ["is_playing"] = true };
                return true;
            }

            if (command.Status == InvokeJobStatus.Pending)
                command.Status = InvokeJobStatus.Running;

            return false;
        }
    }
}
