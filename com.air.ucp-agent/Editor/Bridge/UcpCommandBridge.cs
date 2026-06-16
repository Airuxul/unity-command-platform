using System.Collections.Generic;
using System.Linq;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Dispatch;
using Air.UcpAgent.Execution;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.IO;
using Air.UcpAgent.Job;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Editor.Bridge
{
    static class PendingDeferredCommands
    {
        sealed class PendingEntry
        {
            public string UcpCommandId;
            public string DeferredJobId;
            public string OutboxPath;
        }

        static readonly Dictionary<string, PendingEntry> Pending = new Dictionary<string, PendingEntry>();

        public static void Track(UcpCommand command, string projectId, string deferredJobId)
        {
            if (command == null || string.IsNullOrEmpty(command.id) || string.IsNullOrEmpty(deferredJobId))
                return;

            Pending[command.id] = new PendingEntry
            {
                UcpCommandId = command.id,
                DeferredJobId = deferredJobId,
                OutboxPath = UcpPaths.OutboxFile(projectId, command.id),
            };
        }

        public static bool HasPending => Pending.Count > 0;

        public static void Tick()
        {
            if (Pending.Count == 0)
                return;

            foreach (var pair in Pending.ToList())
            {
                var entry = pair.Value;
                var job = EditorJobStateManager.Get(entry.DeferredJobId);
                if (job == null)
                    continue;

                if (job.Status is InvokeJobStatus.Pending or InvokeJobStatus.Running)
                    continue;

                var result = UcpResultMapper.FromJob(entry.UcpCommandId, job);
                UcpPaths.WriteJsonAtomic(entry.OutboxPath, JsonConvert.SerializeObject(result, Formatting.Indented));
                Pending.Remove(pair.Key);
            }
        }
    }

    public sealed class UcpCliCommandHandler : ICommandHandler
    {
        readonly IInvokeHandler _handler;

        public UcpCliCommandHandler(IInvokeHandler handler) => _handler = handler;

        public string CommandType => _handler.Name;

        public UcpCommandExecution Execute(UcpCommand command, string projectId)
        {
            var request = new InvokeRequest
            {
                Command = CommandType,
                Parameters = NormalizeArgs(command.args),
                RequestId = command.id,
                Endpoint = "editor",
            };

            var post = EditorInvokeHost.Instance.HandleCommand(request);
            if (post.HoldConnectionUntilComplete)
            {
                PendingDeferredCommands.Track(command, projectId, post.CommandId);
                return UcpCommandExecution.Deferred();
            }

            return UcpCommandExecution.Completed(UcpResultMapper.FromPost(command.id, post));
        }

        public UcpResult Execute(UcpCommand command) => Execute(command, projectId: null).Result;

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
    }

    public sealed class UcpCommandExecution
    {
        public UcpResult Result { get; private set; }
        public bool IsDeferred { get; private set; }

        public static UcpCommandExecution Completed(UcpResult result) =>
            new UcpCommandExecution { Result = result, IsDeferred = false };

        public static UcpCommandExecution Deferred() =>
            new UcpCommandExecution { Result = null, IsDeferred = true };
    }

    static class UcpCommandRegistrar
    {
        const string EditorHost = "editor";

        public static void RegisterEditorCommands(CommandHandlerRegistry registry)
        {
            foreach (var handler in CliCommandDiscovery.Handlers)
            {
                if (!InvokeAvailability.IsAvailableForHost(handler.Scope, EditorHost))
                    continue;

                registry.Register(new UcpCliCommandHandler(handler));
            }
        }
    }
}
