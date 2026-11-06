using System;
using System.Collections.Generic;
using Air.UcpAgent.Commands;
using Air.UcpAgent.Params;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Cli
{
    /// <summary>
    /// Runs one <see cref="UcpRequest"/> on a host: resolve, bind, run, and map how it finished.
    /// Shared by the Editor file queue and the runtime HTTP agent. A group runs each of its steps
    /// through <see cref="Invoke"/>, the part below resolving and binding.
    /// </summary>
    public static class CommandExecutor
    {
        /// <returns>
        /// The result, or null when the command continues as a deferred job in
        /// <paramref name="jobs"/>; the Editor writes that result when the job finishes.
        /// </returns>
        public static UcpResult Execute(string host, UcpRequest request, IDeferredJobs jobs)
        {
            var started = DateTime.UtcNow;
            var result = Run(host, request, jobs);
            if (result != null)
                result.duration = (int)(DateTime.UtcNow - started).TotalMilliseconds;

            return result;
        }

        static UcpResult Run(string host, UcpRequest request, IDeferredJobs jobs)
        {
            var entry = CommandRegistry.Find(host, request.type);
            if (entry == null)
            {
                return UcpResults.Failure(request.id, new CommandFailure(
                    UcpErrorCodes.UnknownCommand,
                    "Handler not found: " + request.type,
                    $"Run {CommandNames.Help} to list the commands on this host."));
            }

            var (parameters, bindError) = Bind(entry, NormalizeArgs(request.args));
            if (bindError != null)
                return UcpResults.Failure(request.id, bindError);

            if (entry.Completion != null && jobs == null)
            {
                return UcpResults.Failure(request.id, new CommandFailure(
                    UcpErrorCodes.CommandFailed,
                    $"'{entry.Name}' cannot defer on this host."));
            }

            // The host's deadline when it sent one, so a job gives up exactly when the host does;
            // `timeout` was measured before delivery and runs out a little later.
            var timeoutMs = request.deadline > 0
                ? (int)Math.Max(1, request.deadline - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
                : request.timeout > 0 ? request.timeout : entry.DefaultTimeoutMs;
            if (entry.Completion != null && timeoutMs <= 0)
            {
                return UcpResults.Failure(request.id, new CommandFailure(
                    UcpErrorCodes.InvalidArgument,
                    $"'{entry.Name}' needs a deadline."));
            }

            var ctx = entry.CreateContext();
            ctx.Bind(host, request.id, entry.Name, timeoutMs, jobs);
            var payload = Invoke(entry, parameters, ctx);

            if (ctx.IsCompleted)
            {
                if (ctx.HasJob)
                    jobs.Remove(request.id);

                return UcpResults.From(request.id, ctx, ctx.Message ?? entry.Name);
            }

            // Deferred descriptor, or an explicit Defer: the job finishes later. A null return is not success.
            if (ctx.Deferred || entry.Completion != null)
                return null;

            return UcpResults.Success(request.id, ctx.Message ?? entry.Name, payload);
        }

        /// <summary>Binds raw arguments to the command's parameter type; null parameters for a command without any.</summary>
        public static (object parameters, CommandFailure error) Bind(CommandEntry entry, Dictionary<string, object> args)
        {
            if (entry.ParamType == null)
                return (null, null);

            var (bound, error) = UcpParamBinder.Bind(entry.ParamType, args);
            if (error == null)
                return (bound, null);

            return (null, new CommandFailure(
                UcpErrorCodes.InvalidArgument,
                error,
                $"Run {CommandNames.Help} with command={entry.Name} for its parameters."));
        }

        /// <summary>
        /// Runs a bound command on <paramref name="ctx"/>, which the caller created with
        /// <see cref="CommandEntry.CreateContext"/> and bound. When it ends synchronously the context
        /// holds the result or the failure; a deferred command's job was begun before Execute ran.
        /// </summary>
        /// <returns>What Execute returned.</returns>
        public static object Invoke(CommandEntry entry, object parameters, CommandContext ctx)
        {
            object payload = null;
            try
            {
                // Before Execute: the command may hand its id to something that completes the job
                // (ScriptCompilationService), and the domain may reload right after Execute returns.
                if (entry.Completion != null)
                    BeginJob(entry, ctx);

                payload = ExecutionHost.Current.Run(entry, parameters, ctx);

                if (entry.Completion != null &&
                    ctx.DeferredKind != null &&
                    ctx.DeferredKind != entry.Completion)
                {
                    throw new InvalidOperationException(
                        $"'{entry.Name}' waits for '{entry.Completion}', not '{ctx.DeferredKind}'.");
                }
            }
            catch (Exception ex)
            {
                ctx.Fail(CommandFailure.From(CommandRegistry.Unwrap(ex)));
            }

            ctx.ReturnedFromRun = true;
            if (!ctx.IsCompleted && !ctx.Deferred && entry.Completion == null)
                ctx.Succeed(payload);
            return payload;
        }

        static void BeginJob(CommandEntry entry, CommandContext ctx) => ctx.Defer(entry.Completion);

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
