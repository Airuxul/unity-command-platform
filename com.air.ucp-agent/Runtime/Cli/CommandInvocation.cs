using System;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// Where a deferred command's job lives. The Editor keeps jobs across domain reloads; the
    /// runtime agent has none, so a command that does not finish synchronously fails there.
    /// </summary>
    public interface IDeferredJobs
    {
        /// <summary>Creates the job, or retargets an existing one. Throws if the kind has no completion policy.</summary>
        /// <param name="completionArgs">Kept on the job for the policy to read; null leaves it as it was.</param>
        void Begin(string id, string command, string completionKind, int timeoutMs, string completionArgs = null);

        bool Has(string id);
        void Remove(string id);
        void MarkRunning(string id);
        void Succeed(string id, object result);
        void Fail(string id, CommandFailure failure);
    }

    /// <summary>One run of one command: its id, and how it finished.</summary>
    public sealed class CommandInvocation
    {
        readonly IDeferredJobs _jobs;

        public CommandInvocation(string commandId, string command, int timeoutMs, IDeferredJobs jobs)
        {
            CommandId = commandId;
            Command = command;
            TimeoutMs = timeoutMs;
            _jobs = jobs;
        }

        public string CommandId { get; }
        public string Command { get; }
        public int TimeoutMs { get; }

        public bool IsCompleted { get; private set; }
        public object Result { get; private set; }

        /// <summary>Null unless the command failed.</summary>
        public CommandFailure Failure { get; private set; }

        /// <summary>True once a job exists for this run: it finishes through the job, not synchronously.</summary>
        public bool HasJob => _jobs != null && _jobs.Has(CommandId);

        /// <summary>Set by the executor once Run returned; completions after that go to the job.</summary>
        public bool ReturnedFromRun { get; set; }

        public void Succeed(object result)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            Result = result;
            if (ReturnedFromRun && HasJob)
                _jobs.Succeed(CommandId, result);
        }

        public void Fail(CommandFailure failure)
        {
            if (IsCompleted)
                return;

            IsCompleted = true;
            Failure = failure ?? new CommandFailure(UcpErrorCodes.CommandFailed, null);
            if (ReturnedFromRun && HasJob)
                _jobs.Fail(CommandId, Failure);
        }

        public void MarkRunning()
        {
            if (HasJob)
                _jobs.MarkRunning(CommandId);
        }

        public void Defer(string completionKind, string completionArgs = null)
        {
            if (IsCompleted)
                throw new InvalidOperationException($"'{Command}' already completed; it cannot defer.");
            if (_jobs == null)
                throw new InvalidOperationException($"'{Command}' cannot defer on this host.");

            _jobs.Begin(CommandId, Command, completionKind, TimeoutMs, completionArgs);
        }
    }
}
