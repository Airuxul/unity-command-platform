using Air.UnityConnector.Job;
using Air.UnityConnector.Http;

namespace Air.UnityConnector
{
    /// <summary>Host-specific command dispatch (sync 200 vs deferred 202 pipeline).</summary>
    public interface IInvokeHost
    {
        string HostName { get; }
        InvokePipeline.PostResult HandleCommand(InvokeRequest request);
    }
}
