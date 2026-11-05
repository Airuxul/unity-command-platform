using System.Collections.Generic;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Cli
{
    /// <summary>How a finished command becomes the <see cref="UcpResult"/> the caller reads.</summary>
    public static class UcpResults
    {
        public static UcpResult From(string commandId, CommandInvocation invocation)
        {
            if (invocation.Failure != null)
                return Failure(commandId, invocation.Failure);

            var (data, _, message) = Unwrap(invocation.Result);
            return new UcpResult
            {
                id = commandId,
                success = true,
                message = message,
                data = ToDictionary(data),
            };
        }

        public static UcpResult FromJob(string commandId, InvokeJobRecord job)
        {
            if (job == null)
                return Failure(commandId, new CommandFailure("job_not_found", "Job not found"));

            if (job.Status == InvokeJobStatus.Succeeded)
            {
                var (data, code, message) = Unwrap(job.Result ?? FromJson(job.ResultJson));
                return new UcpResult
                {
                    id = commandId,
                    success = true,
                    message = message ?? code ?? "ok",
                    data = ToDictionary(data),
                };
            }

            // Records from before 0.5 carry no code; their status is the closest thing to one.
            var fallbackCode = job.Status == InvokeJobStatus.Orphaned
                ? UcpErrorCodes.Orphaned
                : UcpErrorCodes.CommandFailed;
            return Failure(commandId, new CommandFailure(
                string.IsNullOrEmpty(job.ErrorCode) ? fallbackCode : job.ErrorCode,
                job.Error,
                string.IsNullOrEmpty(job.Hint) ? null : job.Hint,
                job.Result ?? FromJson(job.ResultJson)));
        }

        /// <remarks><c>error</c> carries the code; agents before 0.5 put the message text there.</remarks>
        public static UcpResult Failure(string commandId, CommandFailure failure) =>
            new UcpResult
            {
                id = commandId,
                success = false,
                error = failure.Code,
                message = failure.Message,
                hint = failure.Hint,
                data = ToDictionary(failure.Data),
            };

        static (object data, string code, string message) Unwrap(object result) =>
            result is InvokeResult invoke ? (invoke.Payload, invoke.Code, invoke.Message) : (result, null, null);

        /// <summary>
        /// A job that finished just before a domain reload comes back with only ResultJson. An
        /// InvokeResult serialised there is turned back into one so its payload is not nested.
        /// </summary>
        static object FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            var token = JToken.Parse(json);
            if (token is JObject obj && obj.ContainsKey(nameof(InvokeResult.Payload)))
            {
                return new InvokeResult
                {
                    Code = (string)obj[nameof(InvokeResult.Code)],
                    Message = (string)obj[nameof(InvokeResult.Message)],
                    Payload = obj[nameof(InvokeResult.Payload)] is { Type: not JTokenType.Null } payload
                        ? payload
                        : null,
                };
            }

            return token;
        }

        static Dictionary<string, object> ToDictionary(object value) =>
            value switch
            {
                null => null,
                Dictionary<string, object> dict => dict,
                JObject obj => obj.ToObject<Dictionary<string, object>>(),
                _ => new Dictionary<string, object> { ["value"] = value },
            };
    }
}
