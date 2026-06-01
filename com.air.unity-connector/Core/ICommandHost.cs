using UnityCliConnector.Http;

namespace UnityCliConnector
{
    /// <summary>Host-specific command dispatch (sync 200 vs deferred 202 pipeline).</summary>
    public interface ICommandHost
    {
        string HostName { get; }
        CommandPipeline.PostResult HandleCommand(CommandRequest request);
    }
}
