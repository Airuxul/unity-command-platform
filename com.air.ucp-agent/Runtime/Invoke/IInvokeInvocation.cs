using System.Collections.Generic;

namespace Air.UcpAgent.Invoke
{
    public interface IInvokeInvocation
    {
        string Name { get; }
        void Invoke(IInvocationContext context, IReadOnlyDictionary<string, object> arguments);
    }
}
