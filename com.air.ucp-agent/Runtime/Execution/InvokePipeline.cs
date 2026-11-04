using System;
using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;

namespace Air.UcpAgent.Execution
{
    public static class InvokePipeline
    {
        public sealed class PostResult
        {
            public int StatusCode;
            public Dictionary<string, object> Body;
            /// <summary>Keep HTTP connection open until job completes (CONN-10).</summary>
            public bool HoldConnectionUntilComplete;
            public string CommandId;
        }

        public static bool HoldsConnectionUntilComplete(string completionKind) =>
            completionKind is InvokeCompletionCatalog.CompletionCompilation
                or InvokeCompletionCatalog.CompletionEnterPlay
                or InvokeCompletionCatalog.CompletionExitPlay
                or InvokeCompletionCatalog.CompletionDeferred;

        public static PostResult HandleUnifiedPost(
            InvokeRequest request,
            Func<InvokeRequest, string, (string commandId, InvokeContext context)> createContext,
            Action<InvokeContext, Dictionary<string, object>> execute)
        {
            // Outside the try so a failure still reports the job it created, and the host can drop it.
            string commandId = null;
            try
            {
                var completion = InvokeCompletionCatalog.GetCompletionKind(request.Command)
                    ?? InvokeCompletionCatalog.CompletionDeferred;
                InvokeContext context;
                (commandId, context) = createContext(request, completion);

                execute(context, request.Parameters);

                if (context != null && context.IsCompleted)
                    return BuildCompletedResult(context, request.RequestId, commandId);

                if (HoldsConnectionUntilComplete(completion))
                {
                    return new PostResult
                    {
                        HoldConnectionUntilComplete = true,
                        CommandId = commandId,
                    };
                }

                return BuildFailure(
                    "command did not complete synchronously",
                    request.RequestId,
                    commandId);
            }
            catch (Exception ex)
            {
                return BuildFailure(ex.Message, request.RequestId, commandId);
            }
        }

        static PostResult BuildCompletedResult(InvokeContext context, string requestId, string commandId)
        {
            var ok = string.IsNullOrEmpty(context.CompletedError);
            var (data, code, message) = UnwrapResult(context.CompletedResult);
            var body = new Dictionary<string, object>
            {
                ["ok"] = ok,
                ["data"] = data,
                ["error"] = context.CompletedError,
                ["request_id"] = requestId,
                ["command_id"] = commandId,
            };
            if (!string.IsNullOrWhiteSpace(code))
                body["code"] = code;
            if (!string.IsNullOrWhiteSpace(message))
                body["message"] = message;
            else if (!ok)
            {
                // A failing command has no result to carry a message, so mirror the error text into
                // `message`. Without this the reason a command failed only reaches the caller as an
                // error *code*, and anything human-readable in it is lost.
                body["message"] = context.CompletedError;
            }

            return new PostResult
            {
                StatusCode = ok ? 200 : 400,
                Body = body,
                CommandId = commandId,
            };
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

            return new PostResult { StatusCode = 400, Body = body, CommandId = commandId };
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
