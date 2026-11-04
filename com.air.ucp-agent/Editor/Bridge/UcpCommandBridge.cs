using System.Collections.Generic;
using Air.UcpAgent.Cli;
using Air.UcpAgent.Dispatch;
using Air.UcpAgent.Invoke;
using Air.UcpAgent.Job;
using Air.UcpAgent.Protocol;
using Newtonsoft.Json.Linq;

namespace Air.UcpAgent.Editor.Bridge
{
    public sealed class UcpCliCommandHandler : ICommandHandler
    {
        readonly IInvokeHandler _handler;

        public UcpCliCommandHandler(IInvokeHandler handler) => _handler = handler;

        public string CommandType => _handler.Name;

        /// <summary>
        /// Runs a file-queue command. A deferred one keeps its job (RequestId = command id), and
        /// <see cref="EditorAgentBootstrap"/> writes the outbox when that job finishes, including
        /// after a domain reload.
        /// </summary>
        public UcpCommandExecution Dispatch(UcpCommand command)
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
                return UcpCommandExecution.Deferred();

            EditorJobStateManager.Remove(post.CommandId);
            return UcpCommandExecution.Completed(UcpResultMapper.FromPost(command.id, post));
        }

        public UcpResult Execute(UcpCommand command) => Dispatch(command).Result;

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

                registry.Register(new UcpCliCommandHandler(handler), handler.Aliases);
            }
        }
    }
}
