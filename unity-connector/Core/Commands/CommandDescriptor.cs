using System;

namespace UnityCliConnector.Commands
{
    public class CommandDescriptor
    {
        public string Name { get; }
        public CommandScope Scope { get; }
        public string Description { get; }
        public string[] Aliases { get; }
        public int DefaultTimeoutMs { get; }
        public bool AllowConnectionRetry { get; }
        public Type ParamType { get; }

        public CommandDescriptor(
            string name,
            CommandScope scope,
            string description,
            Type paramType = null,
            string[] aliases = null,
            int defaultTimeoutMs = 0,
            bool allowConnectionRetry = true)
        {
            Name = name;
            Scope = scope;
            Description = description ?? "";
            ParamType = paramType;
            Aliases = aliases ?? Array.Empty<string>();
            DefaultTimeoutMs = defaultTimeoutMs;
            AllowConnectionRetry = allowConnectionRetry;
        }
    }

    public class CommandDescriptor<TParams> : CommandDescriptor
    {
        public CommandDescriptor(
            string name,
            CommandScope scope,
            string description,
            string[] aliases = null,
            int defaultTimeoutMs = 0,
            bool allowConnectionRetry = true)
            : base(name, scope, description, typeof(TParams), aliases, defaultTimeoutMs, allowConnectionRetry)
        {
        }
    }

    public class DeferredCommandDescriptor : CommandDescriptor
    {
        public string Completion { get; }

        public DeferredCommandDescriptor(
            string name,
            CommandScope scope,
            string description,
            string completion = null,
            Type paramType = null,
            string[] aliases = null,
            int defaultTimeoutMs = 0,
            bool allowConnectionRetry = true)
            : base(name, scope, description, paramType, aliases, defaultTimeoutMs, allowConnectionRetry)
        {
            Completion = string.IsNullOrEmpty(completion)
                ? CommandCompletionCatalog.CompletionDeferred
                : completion;
        }
    }

    public sealed class DeferredCommandDescriptor<TParams> : DeferredCommandDescriptor
    {
        public DeferredCommandDescriptor(
            string name,
            CommandScope scope,
            string description,
            string completion = null,
            string[] aliases = null,
            int defaultTimeoutMs = 0,
            bool allowConnectionRetry = true)
            : base(name, scope, description, completion, typeof(TParams), aliases, defaultTimeoutMs, allowConnectionRetry)
        {
        }
    }
}
