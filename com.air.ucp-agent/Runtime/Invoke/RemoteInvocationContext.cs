using System;
using Air.UcpAgent.Job;

namespace Air.UcpAgent.Invoke
{
    public sealed class RemoteInvocationContext : IInvocationContext
    {
        public InvokeContext Context { get; }

        public RemoteInvocationContext(InvokeContext context) =>
            Context = context ?? throw new ArgumentNullException(nameof(context));
    }
}
