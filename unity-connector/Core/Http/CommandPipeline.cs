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

        public static PostResult HandleUnifiedPost(
            CommandRequest request,
            Func<CommandRequest, string, (string commandId, CommandContext context)> createContext,
            Action<CommandContext, Dictionary<string, object>> execute)
        {
            try
            {
                var completion = CommandCompletionCatalog.GetCompletionKind(request.Command)
                    ?? CommandCompletionCatalog.CompletionDeferred;
                var (commandId, context) = createContext(request, completion);

                execute(context, request.Parameters);

                if (context != null && context.IsCompleted)
                {
                    var ok = string.IsNullOrEmpty(context.CompletedError);
                    return new PostResult
                    {
                        StatusCode = ok ? 200 : 400,
                        Body = new Dictionary<string, object>
                        {
                            ["ok"] = ok,
                            ["data"] = context.CompletedResult,
                            ["error"] = context.CompletedError,
                            ["request_id"] = request.RequestId,
                            ["command_id"] = commandId,
                        },
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
    }
}
