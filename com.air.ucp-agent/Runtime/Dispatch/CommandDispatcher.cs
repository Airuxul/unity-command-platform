using System;
using System.Collections.Generic;
using Air.UcpAgent.Protocol;

namespace Air.UcpAgent.Dispatch
{
    public interface ICommandHandler
    {
        string CommandType { get; }
        UcpResult Execute(UcpCommand command);
    }

    public sealed class CommandDispatcher
    {
        readonly Dictionary<string, ICommandHandler> _handlers =
            new Dictionary<string, ICommandHandler>(StringComparer.OrdinalIgnoreCase);

        public void Register(ICommandHandler handler) => _handlers[handler.CommandType] = handler;

        public bool TryGetHandler(string commandType, out ICommandHandler handler) =>
            _handlers.TryGetValue(commandType, out handler);

        public IReadOnlyCollection<string> CommandTypes => _handlers.Keys;

        public bool TryDispatch(UcpCommand command, out UcpResult result)
        {
            result = null;
            if (command == null || string.IsNullOrEmpty(command.type))
                return false;

            if (!_handlers.TryGetValue(command.type, out var handler))
            {
                result = new UcpResult
                {
                    id = command.id,
                    success = false,
                    duration = 0,
                    error = "unknown_command",
                    message = "Handler not found: " + command.type
                };
                return true;
            }

            var started = DateTime.UtcNow;
            try
            {
                result = handler.Execute(command);
                if (result != null && string.IsNullOrEmpty(result.id))
                    result.id = command.id;
            }
            catch (Exception ex)
            {
                result = new UcpResult
                {
                    id = command.id,
                    success = false,
                    duration = (int)(DateTime.UtcNow - started).TotalMilliseconds,
                    error = "handler_exception",
                    message = ex.Message
                };
            }

            if (result != null && result.duration <= 0)
                result.duration = (int)(DateTime.UtcNow - started).TotalMilliseconds;

            return true;
        }
    }
}
