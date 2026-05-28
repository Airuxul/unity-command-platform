using System.Collections.Generic;

namespace UnityCliConnector
{
    public interface ICommandHandler
    {
        string Name { get; }
        CommandScope Scope { get; }
        string Description { get; }
        /// <summary>Non-empty for commands described by <see cref="Commands.DeferredCommandDescriptor"/>.</summary>
        string Completion { get; }
        string[] Aliases { get; }
        int DefaultTimeoutMs { get; }
        bool AllowConnectionRetry { get; }
        string[] ParamDescriptions { get; }
        void ExecuteCommand(CommandContext context, Dictionary<string, object> parameters);
    }
}
