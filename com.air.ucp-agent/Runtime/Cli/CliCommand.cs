// @tag cli-command
using System;
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// Base of every ucp-cli command. Inherit <see cref="CliCommand"/> or <see cref="CliCommand{TParams}"/>;
    /// <see cref="CommandRegistry"/> finds subclasses by reflection.
    /// </summary>
    public abstract class CliCommand
    {
        CommandInvocation _invocation;

        public abstract InvokeDescriptor Descriptor { get; }

        public void Bind(CommandInvocation invocation) => _invocation = invocation;

        protected string CommandId => _invocation?.CommandId;
        protected void CompleteSuccess(object result) => _invocation?.Succeed(result);
        protected void CompleteFail(string error) =>
            _invocation?.Fail(new CommandFailure(UcpErrorCodes.CommandFailed, error));

        /// <summary>Fails with the exception's code (<see cref="UcpErrorCodes.Of"/>), message and hint.</summary>
        protected void CompleteFail(Exception ex) => _invocation?.Fail(CommandFailure.From(ex));

        /// <summary>Fails with a code and detail of the command's own, such as a failed build's report.</summary>
        protected void CompleteFail(CommandFailure failure) => _invocation?.Fail(failure);
        protected void MarkRunning() => _invocation?.MarkRunning();

        /// <summary>
        /// Finish later instead of in <see cref="Run()"/>: the host keeps a job that the completion
        /// policy for <paramref name="completionKind"/> completes, across domain reloads too.
        /// Commands declared with <see cref="DeferredInvokeDescriptor"/> already have one.
        /// </summary>
        protected void Defer(string completionKind) => Defer(completionKind, null);

        /// <inheritdoc cref="Defer(string)"/>
        /// <param name="completionArgs">What the policy reads back from <see cref="Job.InvokeJobRecord.CompletionArgs"/>.</param>
        protected void Defer(string completionKind, string completionArgs) =>
            (_invocation ?? throw new InvalidOperationException("Command is not running.")).Defer(completionKind, completionArgs);

        public abstract void Run();

        /// <summary>Called by <see cref="CommandExecutor"/> with the bound parameters.</summary>
        public virtual void Run(object parameters) => Run();
    }

    /// <summary>ucp-cli command with bound parameters.</summary>
    public abstract class CliCommand<TParams> : CliCommand
    {
        public abstract void Run(TParams parameters);

        public sealed override void Run() =>
            throw new InvalidOperationException("Use Run(TParams) for parameterized CLI commands.");

        public sealed override void Run(object parameters) => Run((TParams)parameters);
    }
}
