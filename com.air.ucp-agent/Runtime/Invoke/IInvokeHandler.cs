using System;
using System.Collections.Generic;
using Air.UcpAgent.Job;

namespace Air.UcpAgent.Invoke
{
    public interface IInvokeHandler : IInvokeInvocation
    {
        CommandHostScope Scope { get; }
        string Description { get; }
        string Completion { get; }
        string[] Aliases { get; }
        int DefaultTimeoutMs { get; }
        bool AllowConnectionRetry { get; }
        string[] ParamDescriptions { get; }
        Type ParamType { get; }

        void InvokeRemote(InvokeContext context, Dictionary<string, object> parameters);
    }
}
