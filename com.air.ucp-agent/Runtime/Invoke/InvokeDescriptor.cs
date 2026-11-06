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
        public Type ParamType { get; }

        /// <summary>
        /// This command still runs while the Editor is busy with another command or a test run.
        /// The host does not hold it behind the session queue.
        /// </summary>
        public bool RunsWhileBusy { get; set; }

        /// <summary>
        /// An edit-target command that only reads. The target is opened without an undo group and
        /// never saved, and a group has nothing to take back for it.
        /// </summary>
        public bool ReadsTargetOnly { get; set; }

        /// <summary>
        /// Command names run before this one, each as its own command. The caller's arguments
        /// stay on this command. Empty by default.
        /// </summary>
        public string[] Before { get; set; } = Array.Empty<string>();

        /// <summary>Command names run after this one succeeds. Empty by default.</summary>
        public string[] After { get; set; } = Array.Empty<string>();

        public InvokeDescriptor(
            string name,
            CommandHostScope scope,
            string description,
            Type paramType = null,
            string[] aliases = null,
            int defaultTimeoutMs = 0)
        {
            Name = name;
            Scope = scope;
            Description = description ?? "";
            ParamType = paramType;
            Aliases = aliases ?? Array.Empty<string>();
            DefaultTimeoutMs = defaultTimeoutMs;
        }
    }

    public class InvokeDescriptor<TParams> : InvokeDescriptor
    {
        public InvokeDescriptor(
            string name,
            CommandHostScope scope,
            string description,
            string[] aliases = null,
            int defaultTimeoutMs = 0)
            : base(name, scope, description, typeof(TParams), aliases, defaultTimeoutMs)
        {
        }
    }

    public class DeferredInvokeDescriptor : InvokeDescriptor
    {
        /// <summary>The policy class this command waits on. The job file stores its full name.</summary>
        public Type Policy { get; }

        public string Completion { get; }

        public DeferredInvokeDescriptor(
            string name,
            CommandHostScope scope,
            string description,
            Type policy,
            Type paramType = null,
            string[] aliases = null,
            int defaultTimeoutMs = 0)
            : base(name, scope, description, paramType, aliases, defaultTimeoutMs)
        {
            if (policy == null)
                throw new ArgumentException("A deferred command names its completion policy.", nameof(policy));

            Policy = policy;
            Completion = PolicyNames.Of(policy);
        }
    }

    public sealed class DeferredInvokeDescriptor<TParams> : DeferredInvokeDescriptor
    {
        public DeferredInvokeDescriptor(
            string name,
            CommandHostScope scope,
            string description,
            Type policy,
            string[] aliases = null,
            int defaultTimeoutMs = 0)
            : base(name, scope, description, policy, typeof(TParams), aliases, defaultTimeoutMs)
        {
        }
    }
}
