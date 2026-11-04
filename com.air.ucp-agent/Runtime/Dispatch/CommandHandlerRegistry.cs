using System;
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

        public void Register(ICommandHandler handler) => Register(handler, null);

        /// <summary>
        /// Registers a handler under its own name plus any aliases. Aliases must be advertised as
        /// capabilities too — ucp-host rejects a command type that is not in the session's
        /// capability list before it ever reaches the agent.
        /// </summary>
        public void Register(ICommandHandler handler, IEnumerable<string> aliases)
        {
            if (handler == null)
                return;

            _dispatcher.Register(handler);
            AddCapability(handler.CommandType);

            foreach (var alias in aliases ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(alias))
                    continue;

                _dispatcher.Register(alias, handler);
                AddCapability(alias);
            }
        }

        void AddCapability(string type)
        {
            if (string.IsNullOrEmpty(type))
                return;

            if (!_capabilities.Exists(c => string.Equals(c, type, StringComparison.OrdinalIgnoreCase)))
                _capabilities.Add(type);
        }
    }
}
