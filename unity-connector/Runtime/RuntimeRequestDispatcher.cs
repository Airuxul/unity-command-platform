using System;
using System.Collections.Generic;
using UnityCliConnector.Http;

namespace UnityCliConnector
{
    public sealed class RuntimeRequestDispatcher : IRequestDispatcher
    {
        public bool TryDispatch(string method, string path, string body, Action<int, Dictionary<string, object>> writeJson)
        {
            if (path == "/health" && method == "GET")
            {
                writeJson(200, new Dictionary<string, object> { ["ok"] = true, ["host"] = "runtime" });
                return true;
            }

            if (path == "/command" && method == "POST")
            {
                var request = CommandHttpHelper.ParseCommandRequest(body, "runtime");
                var result = CommandRouter.Route(request, true, "runtime");
                writeJson(result.Ok ? 200 : 400, new Dictionary<string, object>
                {
                    ["ok"] = result.Ok,
                    ["data"] = result.Data,
                    ["error"] = result.Error,
                    ["request_id"] = result.RequestId,
                });
                return true;
            }

            return false;
        }
    }
}
