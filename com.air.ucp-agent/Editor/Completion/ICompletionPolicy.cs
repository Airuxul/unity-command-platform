using Air.UcpAgent.Cli;
using Air.UcpAgent.Job;

namespace Air.UcpAgent
{
    public interface ICompletionPolicy
    {
        /// <returns>True once the job is done: with <paramref name="result"/>, or with <paramref name="failure"/> set.</returns>
        bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out CommandFailure failure);
    }

    /// <summary>A policy that has to clean up when its job passes the deadline, before the job is dropped.</summary>
    public interface IExpiringPolicy
    {
        void Expired(InvokeJobRecord job);
    }

    public sealed class EditorStateSnapshot
    {
        public bool IsCompiling { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public bool ReadyForTools { get; set; }
    }
}
