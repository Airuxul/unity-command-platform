using System.Collections.Generic;

namespace Air.UcpAgent.Dispatch
{
    /// <summary>Central registry for UCP command handlers and advertised capabilities.</summary>
    public sealed class CommandHandlerRegistry
    {
        readonly CommandDispatcher _dispatcher = new CommandDispatcher();
        readonly List<string> _capabilities = new List<string>();

        public CommandDispatcher Dispatcher => _dispatcher;

        public IReadOnlyList<string> Capabilities => _capabilities;

        public void Register(ICommandHandler handler)
        {
            if (handler == null)
                return;

            _dispatcher.Register(handler);
            var type = handler.CommandType;
            if (string.IsNullOrEmpty(type))
                return;

            if (!_capabilities.Exists(c => string.Equals(c, type, System.StringComparison.OrdinalIgnoreCase)))
                _capabilities.Add(type);
        }
    }
}
