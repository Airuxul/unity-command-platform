using System.Collections.Generic;
using Air.UnityConnector.Invoke;
using Air.GameCore.Serialization;

namespace Air.UnityConnector.Job
{
    public static class JobResponseBuilder
    {
        public static Dictionary<string, object> ToResponse(InvokeJobRecord job)
        {
            if (job == null) return null;
            var dict = new Dictionary<string, object>
            {
                ["ok"] = true,
                ["command_id"] = job.Id,
                ["status"] = job.Status.ToString().ToLowerInvariant(),
                ["command"] = job.Command,
                ["error"] = job.Error,
                ["request_id"] = job.RequestId,
            };
            var result = job.Result ?? JsonHost.Deserialize(job.ResultJson);
            if (result is InvokeResult unified)
            {
                dict["result"] = unified.Payload;
                if (!string.IsNullOrWhiteSpace(unified.Code))
                    dict["code"] = unified.Code;
                if (!string.IsNullOrWhiteSpace(unified.Message))
                    dict["message"] = unified.Message;
            }
            else if (result != null)
            {
                dict["result"] = result;
            }

            return dict;
        }
    }
}
