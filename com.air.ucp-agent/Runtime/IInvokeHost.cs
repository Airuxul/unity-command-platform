using Air.UcpAgent.Job;
using Air.UcpAgent.Execution;

namespace Air.UcpAgent
{
    /// <summary>Host-specific command dispatch (CONN-10: POST completes on held connection).</summary>
    public interface IInvokeHost
    {
        string HostName { get; }
        InvokePipeline.PostResult HandleCommand(InvokeRequest request);
    }
}
