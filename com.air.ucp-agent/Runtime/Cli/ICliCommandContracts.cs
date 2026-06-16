// @tag cli-command
using Air.UcpAgent.Invoke;

namespace Air.UcpAgent.Cli
{
    public interface ICliCommandRuntime
    {
        string CommandId { get; }
        void CompleteSuccess(object result);
        void CompleteFail(string error);
        void MarkRunning();
    }

    public interface ICliInvokeDescriptorProvider
    {
        InvokeDescriptor Descriptor { get; }
    }

    /// <summary>CLI command without parameters (ucp-cli / FileQueue).</summary>
    public interface ICliCommand
    {
        void Run();
    }

    /// <summary>CLI command with typed parameters.</summary>
    public interface ICliCommand<TParams>
    {
        void Run(TParams parameters);
    }
}
