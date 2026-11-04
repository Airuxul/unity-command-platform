using System.Collections.Generic;
using Air.UcpAgent.Execution;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Editor.Bridge
{
    static class UcpResultMapper
    {
        public static UcpResult FromPost(string commandId, InvokePipeline.PostResult post)
        {
            var body = post?.Body ?? new Dictionary<string, object>();
            var ok = body.TryGetValue("ok", out var okVal) && okVal is bool b && b;
            body.TryGetValue("message", out var message);
            body.TryGetValue("data", out var data);
            body.TryGetValue("error", out var error);

            return new UcpResult
            {
                id = commandId,
                success = ok && post.StatusCode >= 200 && post.StatusCode < 300,
                duration = 0,
                message = message?.ToString(),
                data = ToDictionary(data),
                error = ok ? null : error?.ToString() ?? message?.ToString() ?? "command_failed",
            };
        }

        public static UcpResult FromJob(string commandId, InvokeJobRecord job)
        {
            if (job == null)
            {
                return new UcpResult
                {
                    id = commandId,
                    success = false,
                    duration = 0,
                    error = "job_not_found",
                    message = "Job not found",
                };
            }

            if (job.Status == InvokeJobStatus.Succeeded)
            {
                var (data, code, message) = Unwrap(job.Result ?? FromJson(job.ResultJson));
                return new UcpResult
                {
                    id = commandId,
                    success = true,
                    duration = 0,
                    message = message ?? code ?? "ok",
                    data = ToDictionary(data),
                };
            }

            return new UcpResult
            {
                id = commandId,
                success = false,
                duration = 0,
                error = job.Status.ToString().ToLowerInvariant(),
                message = job.Error ?? "command_failed",
            };
        }

        static (object data, string code, string message) Unwrap(object result)
        {
            if (result is InvokeResult invoke)
                return (invoke.Payload, invoke.Code, invoke.Message);

            return (result, null, null);
        }

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

        static Dictionary<string, object> ToDictionary(object value)
        {
            if (value == null)
                return null;

            if (value is Dictionary<string, object> dict)
                return dict;

            if (value is JObject jobj)
                return jobj.ToObject<Dictionary<string, object>>();

            return new Dictionary<string, object> { ["value"] = value };
        }
    }
}
