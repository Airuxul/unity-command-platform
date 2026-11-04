using System;
using System.Collections.Generic;
using Air.UcpAgent.Params;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// Runs one <see cref="UcpCommand"/> on a host: resolve, bind, run, and map how it finished.
    /// Shared by the Editor file queue and the runtime HTTP agent.
    /// </summary>
    public static class CommandExecutor
    {
        /// <returns>
        /// The result, or null when the command continues as a deferred job in
        /// <paramref name="jobs"/>; the Editor writes that result when the job finishes.
        /// </returns>
        public static UcpResult Execute(string host, UcpCommand command, IDeferredJobs jobs)
        {
            var started = DateTime.UtcNow;
            var result = Run(host, command, jobs);
            if (result != null)
                result.duration = (int)(DateTime.UtcNow - started).TotalMilliseconds;

            return result;
        }

        static UcpResult Run(string host, UcpCommand command, IDeferredJobs jobs)
        {
            var entry = CommandRegistry.Find(host, command.type);
            if (entry == null)
                return UcpResults.Failure(command.id, "unknown_command", "Handler not found: " + command.type);

            object parameters = null;
            if (entry.ParamType != null)
            {
                var (bound, error) = CliParamBinder.Bind(entry.ParamType, NormalizeArgs(command.args));
                if (error != null)
                    return UcpResults.Failure(command.id, error, error);

                parameters = bound;
            }

            var timeoutMs = command.timeout > 0 ? command.timeout : entry.DefaultTimeoutMs;
            var invocation = new CommandInvocation(command.id, entry.Name, timeoutMs, jobs);
            try
            {
                // Before Run: the command may hand its id to something that completes the job
                // (ScriptCompilationService), and the domain may reload right after Run returns.
                if (entry.Completion != null && jobs != null)
                    jobs.Begin(command.id, entry.Name, entry.Completion, timeoutMs);

                var instance = entry.Create();
                instance.Bind(invocation);
                instance.Run(parameters);
            }
            catch (Exception ex)
            {
                invocation.Fail(CommandRegistry.Unwrap(ex).Message);
            }

            invocation.ReturnedFromRun = true;

            if (invocation.IsCompleted)
            {
                if (invocation.HasJob)
                    jobs.Remove(command.id);

                return UcpResults.From(command.id, invocation);
            }

            if (invocation.HasJob)
                return null;

            return UcpResults.Failure(command.id, "command did not complete synchronously", null);
        }

        /// <summary>Newtonsoft leaves nested JSON as JTokens; commands expect plain values.</summary>
        public static Dictionary<string, object> NormalizeArgs(Dictionary<string, object> args)
        {
            if (args == null || args.Count == 0)
                return new Dictionary<string, object>();

            var normalized = new Dictionary<string, object>(args.Count);
            foreach (var pair in args)
            {
                normalized[pair.Key] = pair.Value switch
                {
                    JValue value => value.Value,
                    JArray array => array.ToObject<object[]>(),
                    JObject obj => obj.ToObject<Dictionary<string, object>>(),
                    _ => pair.Value,
                };
            }

            return normalized;
        }
    }
}
