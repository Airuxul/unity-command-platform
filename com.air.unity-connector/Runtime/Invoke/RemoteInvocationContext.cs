using System;
using Air.UnityConnector.Job;

namespace Air.UnityConnector.Invoke
{
    public sealed class RemoteInvocationContext : IInvocationContext
    {
        public InvokeContext Context { get; }

        public RemoteInvocationContext(InvokeContext context) =>
            Context = context ?? throw new ArgumentNullException(nameof(context));
    }
}
