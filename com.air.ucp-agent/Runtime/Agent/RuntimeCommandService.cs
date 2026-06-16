using System;
using System.Collections.Generic;
using Air.UcpAgent.Execution;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Air.UcpAgent.Runtime
{
    public static class RuntimeCommandService
    {
        const string RuntimeHost = "runtime";

        public static UcpResult Execute(UcpCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.id))
            {
                return new UcpResult
                {
                    id = command?.id,
                    success = false,
                    duration = 0,
                    error = "invalid_command",
                    message = "Command id is required",
                };
            }

            if (!Application.isPlaying)
            {
                return new UcpResult
                {
                    id = command.id,
                    success = false,
                    duration = 0,
                    error = "not_in_play_mode",
                    message = "Runtime commands require Play Mode",
                };
            }

            var started = DateTime.UtcNow;
            try
            {
                var request = new InvokeRequest
                {
                    Command = command.type,
                    Parameters = NormalizeArgs(command.args),
                    RequestId = command.id,
                    Endpoint = RuntimeHost,
                };

                var post = InvokePipeline.HandleUnifiedPost(
                    request,
                    (req, completion) =>
                    {
                        var ctx = new InvokeContext
                        {
                            CommandId = command.id,
                            RequestId = command.id,
                            Command = command.type,
                            HostName = RuntimeHost,
                            Notifier = NoOpInvokeNotifier.Instance,
                        };
                        return (command.id, ctx);
                    },
                    InvokeExecutor.Execute);

                var result = MapPost(command.id, post);
                if (result.duration <= 0)
                    result.duration = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                return result;
            }
            catch (Exception ex)
            {
                return new UcpResult
                {
                    id = command.id,
                    success = false,
                    duration = (int)(DateTime.UtcNow - started).TotalMilliseconds,
                    error = "handler_exception",
                    message = ex.Message,
                };
            }
        }

        public static List<string> BuildCapabilities()
        {
            var caps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var handler in Cli.CliCommandDiscovery.Handlers)
            {
                if (InvokeAvailability.IsAvailableForHost(handler.Scope, RuntimeHost))
                    caps.Add(handler.Name);
            }

            return new List<string>(caps);
        }

        static UcpResult MapPost(string commandId, InvokePipeline.PostResult post)
        {
            var body = post?.Body ?? new Dictionary<string, object>();
            var ok = body.TryGetValue("ok", out var okVal) && okVal is bool b && b;
            body.TryGetValue("message", out var message);
            body.TryGetValue("data", out var data);
            body.TryGetValue("error", out var error);

            return new UcpResult
            {
                id = commandId,
                success = ok && post is { StatusCode: >= 200 and < 300 },
                duration = 0,
                message = message?.ToString(),
                data = ToDictionary(data),
                error = ok ? null : error?.ToString() ?? message?.ToString() ?? "command_failed",
            };
        }

        static Dictionary<string, object> NormalizeArgs(Dictionary<string, object> args)
        {
            if (args == null || args.Count == 0)
                return new Dictionary<string, object>();

            var normalized = new Dictionary<string, object>(args.Count);
            foreach (var pair in args)
                normalized[pair.Key] = NormalizeValue(pair.Value);

            return normalized;
        }

        static object NormalizeValue(object value) =>
            value switch
            {
                JValue jValue => jValue.Value,
                JArray jArray => jArray.ToObject<object[]>(),
                JObject jObject => jObject.ToObject<Dictionary<string, object>>(),
                _ => value,
            };

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

        sealed class NoOpInvokeNotifier : IInvokeJobNotifier
        {
            public static readonly NoOpInvokeNotifier Instance = new();

            public void MarkRunning(string commandId) { }
            public void Succeed(string commandId, object result) { }
            public void Fail(string commandId, string error) { }
        }
    }
}
