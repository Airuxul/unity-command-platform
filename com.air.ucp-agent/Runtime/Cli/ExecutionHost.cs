using System;

namespace Air.UcpAgent.Cli
{
    /// <summary>Parameters that name an edit target. The Editor's <c>EditTargetParams</c> implements it.</summary>
    public interface IEditTargetParams
    {
    }

    /// <summary>The context of a command that edits a target. The Editor's <c>EditTargetContext</c> implements it.</summary>
    public interface IEditTargetContext
    {
    }

    /// <summary>
    /// Runs one command and takes it back. The Editor installs its own: it opens the edit target,
    /// records the step in a Unity undo group and saves the target. The runtime agent has no edit
    /// targets, so it calls the command directly.
    /// </summary>
    public interface IExecutionHost
    {
        /// <summary>Runs the command. When it fails, what it already did is taken back before the error leaves.</summary>
        object Run(CommandEntry entry, object parameters, CommandContext ctx);

        /// <summary>Takes back a command that finished, from what its context recorded.</summary>
        void Undo(CommandEntry entry, object parameters, CommandContext ctx);
    }

    public static class ExecutionHost
    {
        static IExecutionHost _current = new DirectExecution();

        public static IExecutionHost Current
        {
            get => _current;
            set => _current = value ?? new DirectExecution();
        }

        /// <summary>
        /// Calls the command's own <c>Undo</c> after it failed. A failure inside the undo is added to
        /// the original error, because the caller needs both.
        /// </summary>
        public static Exception UndoAfterFailure(CommandEntry entry, object parameters, CommandContext ctx, Exception error)
        {
            if (!entry.CanUndo)
                return error;

            try
            {
                entry.Undo(parameters, ctx);
                return error;
            }
            catch (Exception undoError)
            {
                return UcpException.Wrap(
                    error,
                    $"{error.Message} Undoing it also failed, so it may be half done: {undoError.Message}");
            }
        }

        sealed class DirectExecution : IExecutionHost
        {
            public object Run(CommandEntry entry, object parameters, CommandContext ctx)
            {
                object result;
                try
                {
                    result = entry.Execute(parameters, ctx);
                }
                catch (Exception ex)
                {
                    var error = UndoAfterFailure(entry, parameters, ctx, ex);
                    if (error == ex)
                        throw;
                    throw error;
                }

                if (ctx.HasFailed && entry.CanUndo)
                    entry.Undo(parameters, ctx);
                return result;
            }

            public void Undo(CommandEntry entry, object parameters, CommandContext ctx)
            {
                if (entry.CanUndo)
                    entry.Undo(parameters, ctx);
            }
        }
    }
}
