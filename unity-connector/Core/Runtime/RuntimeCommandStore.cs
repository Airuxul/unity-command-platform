using System.Collections.Generic;
using UnityCliConnector.Http;

namespace UnityCliConnector
{
    internal sealed class RuntimeCommandStore : ICommandQuery
    {
        private readonly string _host;
        public RuntimeCommandStore(string host) => _host = host;

        public Dictionary<string, object> GetCommandResponse(string commandId) =>
            CommandResponseBuilder.ToResponse(RuntimeCommandStateManager.Get(_host, commandId));
    }
}
