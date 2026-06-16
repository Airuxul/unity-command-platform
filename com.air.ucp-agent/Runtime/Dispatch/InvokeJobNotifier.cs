using System;
using Air.UcpAgent.Job;

namespace Air.UcpAgent
{
    public sealed class InvokeJobNotifier : IInvokeJobNotifier
    {
        readonly Action<string> _markRunning;
        readonly Action<string, object> _succeed;
        readonly Action<string, string> _fail;

        public InvokeJobNotifier(
            Action<string> markRunning,
            Action<string, object> succeed,
            Action<string, string> fail)
        {
            _markRunning = markRunning;
            _succeed = succeed;
            _fail = fail;
        }

        public void MarkRunning(string commandId) => _markRunning?.Invoke(commandId);

        public void Succeed(string commandId, object result) => _succeed?.Invoke(commandId, result);

        public void Fail(string commandId, string error) => _fail?.Invoke(commandId, error);
    }
}
