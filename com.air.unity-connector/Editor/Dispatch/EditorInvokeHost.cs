using System.Collections.Generic;
using Air.UnityConnector;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Job;
using Air.UnityConnector.Http;

namespace Air.UnityConnector
{
    public sealed class EditorInvokeHost : IInvokeHost
    {
        public static readonly EditorInvokeHost Instance = new();
        private static readonly IInvokeJobNotifier Notifier = new InvokeJobNotifier(
            EditorJobStateManager.MarkRunning,
            EditorJobStateManager.Succeed,
            EditorJobStateManager.Fail);

        public string HostName => "editor";

        public InvokePipeline.PostResult HandleCommand(InvokeRequest request) =>
            InvokePipeline.HandleUnifiedPost(
                request,
                CreateContext,
                InvokeExecutor.Execute);

        private static (string commandId, InvokeContext context) CreateContext(InvokeRequest request, string completion)
        {
            var command = EditorJobStateManager.Create(request.Command, completion, request.RequestId);
            var ctx = new InvokeContext
            {
                CommandId = command.Id,
                RequestId = request.RequestId,
                Command = request.Command,
                HostName = Instance.HostName,
                Notifier = Notifier,
            };
            return (command.Id, ctx);
        }
    }
}
