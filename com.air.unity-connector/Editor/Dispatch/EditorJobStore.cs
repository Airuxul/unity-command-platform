using System.Collections.Generic;
using Air.UnityConnector.Http;
using Air.UnityConnector.Job;

namespace Air.UnityConnector
{
    internal sealed class EditorJobStore : IJobQuery
    {
        public static readonly EditorJobStore Instance = new();

        public Dictionary<string, object> GetCommandResponse(string commandId)
        {
            var command = EditorJobStateManager.Get(commandId)
                ?? EditorJobLedger.TryLoad(commandId);
            return JobResponseBuilder.ToResponse(command);
        }
    }
}
