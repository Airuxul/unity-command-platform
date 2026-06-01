using System.Collections.Generic;

namespace UnityCliConnector.Http
{
    public interface ICommandQuery
    {
        Dictionary<string, object> GetCommandResponse(string commandId);
    }
}
