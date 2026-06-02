using System;
using System.Collections.Generic;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Job;

namespace Air.UnityConnector.Http
{
    public static class InvokePipeline
    {
        public sealed class PostResult
        {
            public int StatusCode;
            public Dictionary<string, object> Body;
        }

        public static PostResult HandleUnifiedPost(
            InvokeRequest request,
            Func<InvokeRequest, string, (string commandId, InvokeContext context)> createContext,
            Action<InvokeContext, Dictionary<string, object>> execute)
        {
            try
            {
                var completion = InvokeCompletionCatalog.GetCompletionKind(request.Command)
                    ?? InvokeCompletionCatalog.CompletionDeferred;
                var (commandId, context) = createContext(request, completion);

                execute(context, request.Parameters);

                if (context != null && context.IsCompleted)
                {
                    var ok = string.IsNullOrEmpty(context.CompletedError);
                    var (data, code, message) = UnwrapResult(context.CompletedResult);
                    var body = new Dictionary<string, object>
                    {
                        ["ok"] = ok,
                        ["data"] = data,
                        ["error"] = context.CompletedError,
                        ["request_id"] = request.RequestId,
                        ["command_id"] = commandId,
                    };
                    if (!string.IsNullOrWhiteSpace(code))
                        body["code"] = code;
                    if (!string.IsNullOrWhiteSpace(message))
                        body["message"] = message;

                    return new PostResult
                    {
                        StatusCode = ok ? 200 : 400,
                        Body = body,
                    };
                }

                return new PostResult
                {
                    StatusCode = 202,
                    Body = new Dictionary<string, object>
                    {
                        ["ok"] = true,
                        ["command_id"] = commandId,
                        ["request_id"] = request.RequestId,
                    },
                };
            }
            catch (Exception ex)
            {
                return BuildFailure(ex.Message, request.RequestId, null);
            }
        }

        private static PostResult BuildFailure(string error, string requestId, string commandId)
        {
            var body = new Dictionary<string, object>
            {
                ["ok"] = false,
                ["error"] = error ?? "command_failed",
                ["request_id"] = requestId,
            };
            if (!string.IsNullOrEmpty(commandId))
                body["command_id"] = commandId;

            return new PostResult { StatusCode = 400, Body = body };
        }

        private static (object data, string code, string message) UnwrapResult(object result)
        {
            if (result is InvokeResult unified)
            {
                return (unified.Payload, unified.Code, unified.Message);
            }

            return (result, null, null);
        }
    }
}
