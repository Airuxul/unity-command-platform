using System;
using Air.UcpAgent.Invoke;
using Newtonsoft.Json;

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
        /// <param name="parentId">The group this job is a step of; null for a command the caller sent.</param>
        void Begin(string id, string command, string completionKind, int timeoutMs, string completionArgs = null, string parentId = null);

        bool Has(string id);
        void Remove(string id);
        void MarkRunning(string id);
        void Succeed(string id, object result);
        void Fail(string id, CommandFailure failure);
    }

    /// <summary>
    /// One run of one command: its id, the record the command keeps while it runs, and how it
    /// finished. A command that has to remember something for its <c>Undo</c> declares a subclass
    /// and takes it as <c>Execute</c>'s context parameter.
    /// </summary>
    /// <remarks>
    /// A group writes a subclass's fields to disk so it can undo the step after a domain reload.
    /// They must therefore serialize as JSON; a live object is marked <see cref="JsonIgnoreAttribute"/>.
    /// Everything on this base class is per run and is never written.
    /// </remarks>
    public class CommandContext
    {
        IDeferredJobs _jobs;

        /// <summary><see cref="CommandRegistry.EditorHost"/> or <see cref="CommandRegistry.RuntimeHost"/>.</summary>
        [JsonIgnore] public string Host { get; private set; }

        [JsonIgnore] public string CommandId { get; private set; }
        [JsonIgnore] public string Command { get; private set; }
        [JsonIgnore] public int TimeoutMs { get; private set; }

        /// <summary>The group this run is a step of. Null for a command the caller sent on its own.</summary>
        [JsonIgnore] public CommandGroupInfo Group { get; private set; }

        /// <summary>Replaces the default message (the command name) for this run when set.</summary>
        [JsonIgnore] public string Message { get; set; }

        [JsonIgnore] public bool IsCompleted { get; private set; }
        [JsonIgnore] public object Result { get; private set; }

        /// <summary>Null unless the command failed.</summary>
        [JsonIgnore] public CommandFailure Failure { get; private set; }

        /// <summary>The command asked to finish later.</summary>
        [JsonIgnore] public bool Deferred { get; private set; }

        /// <summary>The completion kind passed to <see cref="Defer(string,string)"/>, when it was called.</summary>
        [JsonIgnore] public string DeferredKind { get; private set; }

        /// <summary>Set by the executor once Execute returned; completions after that go to the job.</summary>
        [JsonIgnore] public bool ReturnedFromRun { get; set; }

        /// <summary>True once a job exists for this run: it finishes through the job, not synchronously.</summary>
        [JsonIgnore] public bool HasJob => _jobs != null && _jobs.Has(CommandId);

        [JsonIgnore] public bool HasFailed => IsCompleted && Failure != null;

        /// <summary>The job store this run defers into; null on a host without one.</summary>
        [JsonIgnore] public IDeferredJobs Jobs => _jobs;

        [JsonIgnore] public bool HasDeferred => Deferred;

        /// <summary>Called by the executor before the command runs.</summary>
        public void Bind(string host, string commandId, string command, int timeoutMs, IDeferredJobs jobs, CommandGroupInfo group = null)
        {
            Host = host;
            CommandId = commandId;
            Command = command;
            TimeoutMs = timeoutMs;
            _jobs = jobs;
            Group = group;
        }

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

        public void Fail(Exception ex) => Fail(CommandFailure.From(ex));

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
            {
                throw new UcpException(
                    UcpErrorCodes.CommandFailed,
                    $"'{Command}' cannot defer on this host.");
            }

            if (Deferred && DeferredKind != completionKind)
            {
                throw new InvalidOperationException(
                    $"'{Command}' deferred as '{DeferredKind}' and then as '{completionKind}'.");
            }

            Deferred = true;
            DeferredKind = completionKind;
            _jobs.Begin(CommandId, Command, completionKind, TimeoutMs, completionArgs, Group?.Id);
        }

        /// <summary>Wait for <paramref name="policy"/>. The job stores that class's full name.</summary>
        public void Defer(Type policy, string completionArgs = null) =>
            Defer(PolicyNames.Of(policy), completionArgs);
    }

    /// <summary>What a step of a group knows about the group it runs in.</summary>
    public sealed class CommandGroupInfo
    {
        public CommandGroupInfo(string id, string directory, bool sharesTargets)
        {
            Id = id;
            Directory = directory;
            SharesTargets = sharesTargets;
        }

        public string Id { get; }

        /// <summary>The group's folder. A step that must back a file up before changing it copies it here.</summary>
        public string Directory { get; }

        /// <summary>Consecutive steps on the same edit target share one open target and one save.</summary>
        public bool SharesTargets { get; }

        /// <summary>The edit target the previous step left open. Only the Editor's execution host reads it.</summary>
        public object OpenTarget { get; set; }
    }
}
