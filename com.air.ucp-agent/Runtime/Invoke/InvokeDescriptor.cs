using System;

namespace Air.UcpAgent.Invoke
{
    public class InvokeDescriptor
    {
        public string Name { get; }
        public CommandHostScope Scope { get; }
        public string Description { get; }
        public string[] Aliases { get; }
        public int DefaultTimeoutMs { get; }
        public bool AllowConnectionRetry { get; }
        public Type ParamType { get; }

        public InvokeDescriptor(
            string name,
            CommandHostScope scope,
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

    public class InvokeDescriptor<TParams> : InvokeDescriptor
    {
        public InvokeDescriptor(
            string name,
            CommandHostScope scope,
            string description,
            string[] aliases = null,
            int defaultTimeoutMs = 0,
            bool allowConnectionRetry = true)
            : base(name, scope, description, typeof(TParams), aliases, defaultTimeoutMs, allowConnectionRetry)
        {
        }
    }

    public class DeferredInvokeDescriptor : InvokeDescriptor
    {
        public string Completion { get; }

        public DeferredInvokeDescriptor(
            string name,
            CommandHostScope scope,
            string description,
            string completion = null,
            Type paramType = null,
            string[] aliases = null,
            int defaultTimeoutMs = 0,
            bool allowConnectionRetry = true)
            : base(name, scope, description, paramType, aliases, defaultTimeoutMs, allowConnectionRetry)
        {
            Completion = string.IsNullOrEmpty(completion)
                ? InvokeCompletionCatalog.CompletionDeferred
                : completion;
        }
    }

    public sealed class DeferredInvokeDescriptor<TParams> : DeferredInvokeDescriptor
    {
        public DeferredInvokeDescriptor(
            string name,
            CommandHostScope scope,
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
