namespace UnityCliConnector.Completion
{
    public sealed class CompilationPolicy : ICompletionPolicy
    {
        public string Kind => CommandCompletionCatalog.CompletionCompilation;

        public bool TryComplete(CommandRecord command, EditorStateSnapshot state, out object result, out string error)
        {
            result = null;
            error = null;

            if (state.IsCompiling)
            {
                if (command.Status == CommandStatus.Pending)
                    command.Status = CommandStatus.Running;
                return false;
            }

            if (command.Status == CommandStatus.Running)
            {
                result = new System.Collections.Generic.Dictionary<string, object> { ["compiled"] = true };
                return true;
            }

            if (command.Status == CommandStatus.Pending)
            {
                command.Status = CommandStatus.Running;
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
