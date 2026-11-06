using System.Collections.Generic;
using Air.UcpAgent.Job;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Cli
{
    /// <summary>How a finished command becomes the <see cref="UcpResult"/> the caller reads.</summary>
    public static class UcpResults
    {
        public static UcpResult Success(string commandId, string message, object data) =>
            new UcpResult
            {
                id = commandId,
                success = true,
                message = string.IsNullOrWhiteSpace(message) ? "ok" : message,
                data = ToDictionary(data),
            };

        public static UcpResult From(string commandId, CommandContext ctx, string message)
        {
            if (ctx.Failure != null)
                return Failure(commandId, ctx.Failure);

            return Success(commandId, message, ctx.Result);
        }

        public static UcpResult FromJob(string commandId, InvokeJobRecord job)
        {
            if (job == null)
                return Failure(commandId, new CommandFailure("job_not_found", "Job not found"));

            if (job.Status == InvokeJobStatus.Succeeded)
            {
                return Success(commandId, job.Message, job.Result ?? FromJson(job.ResultJson));
            }

            var code = job.Status == InvokeJobStatus.Orphaned
                ? UcpErrorCodes.Orphaned
                : UcpErrorCodes.CommandFailed;
            if (!string.IsNullOrEmpty(job.ErrorCode))
                code = job.ErrorCode;

            return Failure(commandId, new CommandFailure(
                code,
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

        static object FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return null;

            return JToken.Parse(json);
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
