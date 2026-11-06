using Air.UcpAgent.Cli;
using Air.UcpAgent.Job;
using Air.UcpAgent.Editor.Services;

namespace Air.UcpAgent.Completion
{
    /// <summary>
    /// Fallback completion when <see cref="Air.UcpAgent.Editor.Services.ScriptCompilationService"/> did not
    /// finish the command (e.g. domain reload). Finishes once Unity is not compiling: failed if the
    /// scripts do not compile, else succeeded.
    /// </summary>
    public sealed class CompilationPolicy : ICompletionPolicy
    {
        public bool TryComplete(InvokeJobRecord command, EditorStateSnapshot state, out object result, out CommandFailure failure)
        {
            result = null;
            failure = null;

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

                failure = ScriptCompilationService.CompileFailure();
                if (failure == null)
                    result = Compiled("compilation_idle");
                return true;
            }

            if (command.Status == InvokeJobStatus.Pending)
            {
                failure = ScriptCompilationService.CompileFailure();
                if (failure == null)
                    result = Compiled("already_idle");
                return true;
            }

            return false;
        }

        static System.Collections.Generic.Dictionary<string, object> Compiled(string note) =>
            new() { ["compiled"] = true, ["note"] = note };
    }
}
