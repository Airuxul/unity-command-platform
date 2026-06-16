using Air.UcpAgent.Job;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Editor.Services;

namespace Air.UcpAgent.Completion
{
    /// <summary>
    /// Fallback completion when <see cref="Air.UcpAgent.Editor.Services.ScriptCompilationService"/> did not
    /// finish the command (e.g. domain reload). Succeeds when Unity is not compiling.
    /// </summary>
    public sealed class CompilationPolicy : ICompletionPolicy
    {
        public string Kind => InvokeCompletionCatalog.CompletionCompilation;

        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out string error)
        {
            result = null;
            error = null;

            if (state.IsCompiling)
            {
                if (command.Status == InvokeJobStatus.Pending)
                    command.Status = InvokeJobStatus.Running;
                return false;
            }

            if (command.Status == InvokeJobStatus.Running)
            {
                if (ScriptCompilationService.OwnsActiveCommand(command.Id))
                    return false;

                result = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["compiled"] = true,
                    ["note"] = "compilation_idle",
                };
                return true;
            }

            if (command.Status == InvokeJobStatus.Pending)
            {
                result = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["compiled"] = true,
                    ["note"] = "already_idle",
                };
                return true;
            }

            return false;
        }
    }
}
