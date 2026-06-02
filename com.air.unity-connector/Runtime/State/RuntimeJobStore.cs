using System.Collections.Generic;
using Air.UnityConnector.Http;
using Air.UnityConnector.Job;

namespace Air.UnityConnector
{
    internal sealed class RuntimeJobStore : IJobQuery
    {
        private readonly string _host;
        public RuntimeJobStore(string host) => _host = host;

        public Dictionary<string, object> GetCommandResponse(string commandId) =>
            JobResponseBuilder.ToResponse(RuntimeJobStateManager.Get(_host, commandId));
    }
}
