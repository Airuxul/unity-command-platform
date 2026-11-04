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
            if (!string.IsNullOrEmpty(invocation.Error))
                return Failure(commandId, invocation.Error, invocation.Error);

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
                return Failure(commandId, "job_not_found", "Job not found");

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

            return Failure(commandId, job.Status.ToString().ToLowerInvariant(), job.Error ?? "command_failed");
        }

        public static UcpResult Failure(string commandId, string error, string message) =>
            new UcpResult
            {
                id = commandId,
                success = false,
                error = string.IsNullOrEmpty(error) ? "command_failed" : error,
                message = message ?? error,
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
