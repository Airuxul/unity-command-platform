using Air.UnityConnector.Job;

namespace Air.UnityConnector
{
    public interface ICompletionPolicy
    {
        string Kind { get; }
        bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out string error);
    }

    public sealed class EditorStateSnapshot
    {
        public bool IsCompiling { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsPaused { get; set; }
        public bool ReadyForTools { get; set; }
    }
}
