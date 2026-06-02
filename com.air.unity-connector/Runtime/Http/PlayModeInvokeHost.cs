using System.Collections.Generic;
using Air.UnityConnector.Http;
using Air.UnityConnector.Invoke;
using Air.UnityConnector.Job;

namespace Air.UnityConnector
{
    public sealed class PlayModeInvokeHost : IInvokeHost
    {
        private readonly string _hostName;
        private readonly IInvokeJobNotifier _notifier;

        public PlayModeInvokeHost(string hostName)
        {
            _hostName = hostName;
            _notifier = new InvokeJobNotifier(
                id => RuntimeJobStateManager.MarkRunning(_hostName, id),
                (id, result) => RuntimeJobStateManager.Succeed(_hostName, id, result),
                (id, error) => RuntimeJobStateManager.Fail(_hostName, id, error));
        }

        public string HostName => _hostName;

        public InvokePipeline.PostResult HandleCommand(InvokeRequest request) =>
            InvokePipeline.HandleUnifiedPost(
                request,
                CreateContext,
                InvokeExecutor.Execute);

        private (string commandId, InvokeContext context) CreateContext(
            InvokeRequest request,
            string completion)
        {
            var command = RuntimeJobStateManager.Create(
                _hostName,
                request.Command,
                completion,
                request.RequestId);
            var ctx = new InvokeContext
            {
                CommandId = command.Id,
                RequestId = request.RequestId,
                Command = request.Command,
                HostName = _hostName,
                Notifier = _notifier,
            };
            return (command.Id, ctx);
        }
    }
}
