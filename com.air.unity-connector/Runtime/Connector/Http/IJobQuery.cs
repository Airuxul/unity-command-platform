using System.Collections.Generic;

namespace Air.UnityConnector.Http
{
    public interface IJobQuery
    {
        Dictionary<string, object> GetCommandResponse(string commandId);
    }
}
