using Air.UcpAgent.Cli;
using Air.UcpAgent.Job;
using Air.UcpAgent;
namespace Air.UcpAgent.Completion
{
    public sealed class ExitPlayModePolicy : ICompletionPolicy
    {
        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out CommandFailure failure)
        {
            result = null;
            failure = null;

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
