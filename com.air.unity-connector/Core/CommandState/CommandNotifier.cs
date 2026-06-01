using System;

namespace UnityCliConnector
{
    public sealed class CommandNotifier : ICommandNotifier
    {
        private readonly Action<string> _markRunning;
        private readonly Action<string, object> _succeed;
        private readonly Action<string, string> _fail;

        public CommandNotifier(Action<string> markRunning, Action<string, object> succeed, Action<string, string> fail)
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
