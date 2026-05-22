using System;
using System.Collections.Generic;

namespace UnityCliConnector.Http
{
    public static class CommandPipeline
    {
        public sealed class PostResult
        {
            public int StatusCode;
            public Dictionary<string, object> Body;
        }

        public static PostResult HandlePost(
            CommandRequest request,
            Func<string, Dictionary<string, object>, string> resolveCompletion,
            Func<CommandRequest, string, Dictionary<string, object>> acceptJob,
            Func<CommandRequest, CommandResult> executeSync)
        {
            var completion = resolveCompletion(request.Command, request.Parameters);
            if (completion != null)
            {
                try
                {
                    var accepted = acceptJob(request, completion);
                    return new PostResult { StatusCode = 202, Body = accepted };
                }
                catch (Exception ex)
                {
                    return new PostResult
                    {
                        StatusCode = 500,
                        Body = new Dictionary<string, object>
                        {
                            ["ok"] = false,
                            ["error"] = ex.Message,
                            ["request_id"] = request.RequestId,
                        },
                    };
                }
            }

            var result = executeSync(request);
            return new PostResult
            {
                StatusCode = result.Ok ? 200 : 400,
                Body = new Dictionary<string, object>
                {
                    ["ok"] = result.Ok,
                    ["data"] = result.Data,
                    ["error"] = result.Error,
                    ["request_id"] = result.RequestId,
                },
            };
        }
    }
}
