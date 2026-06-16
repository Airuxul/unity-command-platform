// @tag cli-command
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Cli
{
    /// <summary>Shared ucp-cli runtime binding; inherit <see cref="CliCommand"/> or <see cref="CliCommand{TParams}"/>.</summary>
    public abstract class CliCommand : ICliInvokeDescriptorProvider, ICliCommand
    {
        ICliCommandRuntime _runtime;

        public abstract InvokeDescriptor Descriptor { get; }

        internal void BindRuntime(ICliCommandRuntime runtime) => _runtime = runtime;

        protected string CommandId => _runtime?.CommandId;
        protected void CompleteSuccess(object result) => _runtime?.CompleteSuccess(result);
        protected void CompleteFail(string error) => _runtime?.CompleteFail(error);
        protected void MarkRunning() => _runtime?.MarkRunning();

        public abstract void Run();
    }

    /// <summary>ucp-cli command with bound parameters.</summary>
    public abstract class CliCommand<TParams> : CliCommand, ICliCommand<TParams>
    {
        public abstract void Run(TParams parameters);

        public sealed override void Run() =>
            throw new System.InvalidOperationException(
                "Use Run(TParams) for parameterized CLI commands.");
    }
}
